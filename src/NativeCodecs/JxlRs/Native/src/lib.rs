// Copyright (c) Clinton Ingram and Contributors
// SPDX-License-Identifier: MIT

use std::{
    cell::RefCell,
    ffi::{CString, c_char, c_void},
    io::{self, BufReader, Read, Seek, SeekFrom},
    panic::{AssertUnwindSafe, catch_unwind},
    ptr, slice,
    sync::{
        Mutex,
        atomic::{AtomicUsize, Ordering},
    },
};

use jxl::{
    api::{
        JxlBitstreamInput, JxlColorType, JxlDataFormat, JxlDecoder, JxlDecoderOptions,
        JxlOutputBuffer, JxlParallelRunner, JxlParallelRunnerFun, JxlPixelFormat, ProcessingResult,
        VisibleFrameSeekTarget, states,
    },
    headers::{extra_channels::ExtraChannel, image_metadata::Orientation},
};
use rayon::iter::{IntoParallelIterator, ParallelIterator};

const VERSION: u32 = 600;
const STREAM_BUFFER_SIZE: usize = 64 * 1024;
const METADATA_BUFFER_SIZE: usize = 4 * 1024;
const CALLBACK_ERROR: usize = usize::MAX;
const SEEK_ERROR: i64 = i64::MIN;

#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct JxlRsImageInfo {
    width: u32,
    height: u32,
    frame_count: u32,
    channels: u32,
    alpha_associated: u32,
    orientation: u32,
    loop_count: u32,
    icc_length: usize,
}

#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct JxlRsFrameInfo {
    duration_milliseconds: f64,
}

type ReadCallback = unsafe extern "C" fn(*mut c_void, *mut u8, usize) -> usize;
type SeekCallback = unsafe extern "C" fn(*mut c_void, i64, i32) -> i64;

#[repr(C)]
pub struct JxlRsStream {
    context: *mut c_void,
    read: Option<ReadCallback>,
    seek: Option<SeekCallback>,
}

#[derive(Clone, Copy)]
struct ScannedFrame {
    duration_milliseconds: f64,
    seek_target: Option<VisibleFrameSeekTarget>,
}

struct PrimedStillDecoder {
    decoder: JxlDecoder<states::WithImageInfo>,
    input_offset: u64,
}

pub struct ScannedImage {
    info: JxlRsImageInfo,
    frames: Vec<ScannedFrame>,
    icc: Vec<u8>,
    alpha_associated: bool,
    parallelism: usize,
    primed_still: Mutex<Option<PrimedStillDecoder>>,
}

struct ParallelRunner {
    max_threads: usize,
}

impl ParallelRunner {
    fn new(max_threads: usize) -> Self {
        Self { max_threads }
    }

    fn as_option(&mut self) -> Option<&mut dyn JxlParallelRunner> {
        if self.max_threads == 1 {
            None
        } else {
            Some(self)
        }
    }
}

impl JxlParallelRunner for ParallelRunner {
    fn run(&mut self, num: usize, fun: &JxlParallelRunnerFun<'_>) -> jxl::error::Result<()> {
        let workers = if self.max_threads == 0 {
            num
        } else {
            num.min(self.max_threads)
        };
        if workers <= 1 {
            return (0..num).try_for_each(fun);
        }

        let next = AtomicUsize::new(0);
        (0..workers).into_par_iter().try_for_each(|_| {
            loop {
                let index = next.fetch_add(1, Ordering::Relaxed);
                if index >= num {
                    return Ok(());
                }
                fun(index)?;
            }
        })
    }
}

struct CallbackStream {
    callbacks: JxlRsStream,
}

impl Read for CallbackStream {
    fn read(&mut self, buffer: &mut [u8]) -> io::Result<usize> {
        let callback = self
            .callbacks
            .read
            .ok_or_else(|| io::Error::other("JPEG XL stream has no read callback"))?;
        // SAFETY: The managed caller guarantees that the callback and context remain valid for
        // the duration of this synchronous native call. `buffer` is writable for its full length.
        let read = unsafe { callback(self.callbacks.context, buffer.as_mut_ptr(), buffer.len()) };
        if read == CALLBACK_ERROR || read > buffer.len() {
            Err(io::Error::other("JPEG XL stream read failed"))
        } else {
            Ok(read)
        }
    }
}

impl Seek for CallbackStream {
    fn seek(&mut self, position: SeekFrom) -> io::Result<u64> {
        let callback = self
            .callbacks
            .seek
            .ok_or_else(|| io::Error::other("JPEG XL stream has no seek callback"))?;
        let (offset, origin) = match position {
            SeekFrom::Start(offset) => (
                i64::try_from(offset).map_err(|_| io::Error::other("Seek offset is too large"))?,
                0,
            ),
            SeekFrom::Current(offset) => (offset, 1),
            SeekFrom::End(offset) => (offset, 2),
        };
        // SAFETY: The managed caller guarantees that the callback and context remain valid for
        // the duration of this synchronous native call.
        let result = unsafe { callback(self.callbacks.context, offset, origin) };
        if result == SEEK_ERROR || result < 0 {
            Err(io::Error::other("JPEG XL stream seek failed"))
        } else {
            Ok(result as u64)
        }
    }
}

thread_local! {
    static LAST_ERROR: RefCell<CString> = RefCell::new(CString::default());
}

fn set_error(message: impl AsRef<str>) {
    let sanitized = message.as_ref().replace('\0', " ");
    LAST_ERROR.with(|slot| {
        *slot.borrow_mut() = CString::new(sanitized).unwrap_or_default();
    });
}

fn orientation_value(orientation: Orientation) -> u32 {
    match orientation {
        Orientation::Identity => 1,
        Orientation::FlipHorizontal => 2,
        Orientation::Rotate180 => 3,
        Orientation::FlipVertical => 4,
        Orientation::Transpose => 5,
        Orientation::Rotate90Cw => 6,
        Orientation::AntiTranspose => 7,
        Orientation::Rotate90Ccw => 8,
    }
}

fn decoder_options(scan_frames_only: bool, premultiply_output: bool) -> JxlDecoderOptions {
    let mut options = JxlDecoderOptions::default();
    options.adjust_orientation = false;
    options.coalescing = true;
    options.desired_intensity_target = Some(1.0);
    options.scan_frames_only = scan_frames_only;
    options.premultiply_output = premultiply_output;
    options
}

fn needs_more_input(input: &mut impl JxlBitstreamInput, context: &str) -> Result<(), String> {
    if input.available_bytes().map_err(|error| error.to_string())? == 0 {
        Err(format!("JPEG XL input is truncated while {context}"))
    } else {
        Ok(())
    }
}

fn read_image_info(
    input: &mut impl JxlBitstreamInput,
    options: JxlDecoderOptions,
    parallelism: usize,
) -> Result<JxlDecoder<states::WithImageInfo>, String> {
    let mut decoder = JxlDecoder::<states::Initialized>::new(options);
    let mut runner = ParallelRunner::new(parallelism);
    loop {
        match decoder
            .process(input, runner.as_option())
            .map_err(|error| error.to_string())?
        {
            ProcessingResult::Complete { result } => return Ok(result),
            ProcessingResult::NeedsMoreInput { fallback, .. } => {
                needs_more_input(input, "reading image metadata")?;
                decoder = fallback;
            }
        }
    }
}

fn read_image_info_from_slice(
    data: &[u8],
    options: JxlDecoderOptions,
    parallelism: usize,
) -> Result<(JxlDecoder<states::WithImageInfo>, u64), String> {
    let mut remaining = data;
    let mut input = &remaining[..remaining.len().min(METADATA_BUFFER_SIZE)];
    let mut decoder = JxlDecoder::<states::Initialized>::new(options);
    let mut runner = ParallelRunner::new(parallelism);
    loop {
        let available_before = input.len();
        let result = decoder
            .process(&mut input, runner.as_option())
            .map_err(|error| error.to_string())?;
        let consumed = available_before
            .checked_sub(input.len())
            .ok_or("JPEG XL input position is invalid")?;
        remaining = remaining
            .get(consumed..)
            .ok_or("JPEG XL input position is invalid")?;

        match result {
            ProcessingResult::Complete { result } => {
                let consumed_total = data
                    .len()
                    .checked_sub(remaining.len())
                    .ok_or("JPEG XL input position is invalid")?;
                return Ok((
                    result,
                    u64::try_from(consumed_total)
                        .map_err(|_| "JPEG XL input offset is too large")?,
                ));
            }
            ProcessingResult::NeedsMoreInput {
                size_hint,
                fallback,
            } => {
                decoder = fallback;
                let retained = input.len();
                let additional = size_hint.max(METADATA_BUFFER_SIZE);
                let exposed = retained.saturating_add(additional).min(remaining.len());
                if exposed == retained {
                    return Err("JPEG XL input is truncated while reading image metadata".into());
                }
                input = &remaining[..exposed];
            }
        }
    }
}

fn read_image_info_from_reader<R: Read + Seek>(
    input: &mut BufReader<R>,
    options: JxlDecoderOptions,
    parallelism: usize,
) -> Result<(JxlDecoder<states::WithImageInfo>, u64), String> {
    let mut buffer = vec![0_u8; METADATA_BUFFER_SIZE];
    let mut start = 0;
    let mut end = Read::read(input, &mut buffer).map_err(|error| error.to_string())?;
    if end == 0 {
        return Err("JPEG XL input is truncated while reading image metadata".into());
    }

    let mut decoder = JxlDecoder::<states::Initialized>::new(options);
    let mut runner = ParallelRunner::new(parallelism);
    loop {
        let mut chunk = &buffer[start..end];
        let available_before = chunk.len();
        let result = decoder
            .process(&mut chunk, runner.as_option())
            .map_err(|error| error.to_string())?;
        start += available_before
            .checked_sub(chunk.len())
            .ok_or("JPEG XL input position is invalid")?;

        match result {
            ProcessingResult::Complete { result } => {
                let stream_position = input.stream_position().map_err(|error| error.to_string())?;
                let unread =
                    u64::try_from(end - start).map_err(|_| "JPEG XL input offset is too large")?;
                return Ok((result, stream_position.saturating_sub(unread)));
            }
            ProcessingResult::NeedsMoreInput {
                size_hint,
                fallback,
            } => {
                decoder = fallback;
                buffer.copy_within(start..end, 0);
                end -= start;
                start = 0;

                let required = end.saturating_add(size_hint.max(METADATA_BUFFER_SIZE));
                if required > buffer.len() {
                    buffer.resize(required, 0);
                }
                let read =
                    Read::read(input, &mut buffer[end..]).map_err(|error| error.to_string())?;
                if read == 0 {
                    return Err("JPEG XL input is truncated while reading image metadata".into());
                }
                end += read;
            }
        }
    }
}

fn read_frame_header(
    mut decoder: JxlDecoder<states::WithImageInfo>,
    input: &mut impl JxlBitstreamInput,
    parallelism: usize,
) -> Result<JxlDecoder<states::WithFrameInfo>, String> {
    let mut runner = ParallelRunner::new(parallelism);
    loop {
        match decoder
            .process(input, runner.as_option())
            .map_err(|error| error.to_string())?
        {
            ProcessingResult::Complete { result } => return Ok(result),
            ProcessingResult::NeedsMoreInput { fallback, .. } => {
                needs_more_input(input, "reading a frame header")?;
                decoder = fallback;
            }
        }
    }
}

fn skip_frame(
    mut decoder: JxlDecoder<states::WithFrameInfo>,
    input: &mut impl JxlBitstreamInput,
) -> Result<JxlDecoder<states::WithImageInfo>, String> {
    loop {
        match decoder
            .skip_frame(input)
            .map_err(|error| error.to_string())?
        {
            ProcessingResult::Complete { result } => return Ok(result),
            ProcessingResult::NeedsMoreInput { fallback, .. } => {
                needs_more_input(input, "scanning a frame")?;
                decoder = fallback;
            }
        }
    }
}

fn image_metadata(
    decoder: &JxlDecoder<states::WithImageInfo>,
    frame_count: u32,
) -> Result<(JxlRsImageInfo, Vec<u8>, bool), String> {
    let basic_info = decoder.basic_info().clone();
    let (width, height) = basic_info.size;
    if width == 0 || height == 0 || width > u32::MAX as usize || height > u32::MAX as usize {
        return Err("JPEG XL image dimensions are not supported".into());
    }

    let alpha_channel = basic_info
        .extra_channels
        .iter()
        .find(|channel| channel.ec_type == ExtraChannel::Alpha);
    let alpha_associated = alpha_channel.is_some_and(|channel| channel.alpha_associated);
    let channels = if alpha_channel.is_some() {
        4
    } else if decoder.current_pixel_format().color_type.is_grayscale() {
        1
    } else {
        3
    };
    let icc = decoder
        .output_color_profile()
        .try_as_icc()
        .map(|profile| profile.into_owned())
        .unwrap_or_default();

    let info = JxlRsImageInfo {
        width: width as u32,
        height: height as u32,
        frame_count,
        channels,
        alpha_associated: u32::from(alpha_associated),
        orientation: orientation_value(basic_info.orientation),
        loop_count: basic_info
            .animation
            .map_or(0, |animation| animation.num_loops),
        icc_length: icc.len(),
    };

    Ok((info, icc, alpha_associated))
}

fn finish_still_image(
    decoder: JxlDecoder<states::WithImageInfo>,
    input_offset: u64,
    parallelism: usize,
) -> Result<ScannedImage, String> {
    let (info, icc, alpha_associated) = image_metadata(&decoder, 1)?;
    Ok(ScannedImage {
        info,
        frames: vec![ScannedFrame {
            duration_milliseconds: 0.0,
            seek_target: None,
        }],
        icc,
        alpha_associated,
        parallelism,
        primed_still: Mutex::new(Some(PrimedStillDecoder {
            decoder,
            input_offset,
        })),
    })
}

fn scan_animated_image(
    input: &mut impl JxlBitstreamInput,
    parallelism: usize,
) -> Result<ScannedImage, String> {
    let mut decoder = read_image_info(input, decoder_options(true, false), parallelism)?;

    while decoder.has_more_frames() {
        let frame_decoder = read_frame_header(decoder, input, parallelism)?;
        decoder = skip_frame(frame_decoder, input)?;
    }

    let frames: Vec<_> = decoder
        .scanned_frames()
        .iter()
        .map(|frame| ScannedFrame {
            duration_milliseconds: frame.duration_ms,
            seek_target: Some(frame.seek_target),
        })
        .collect();
    if frames.is_empty() {
        return Err("JPEG XL image contains no visible frames".into());
    }

    let frame_count = u32::try_from(frames.len()).map_err(|_| "Too many JPEG XL frames")?;
    let (info, icc, alpha_associated) = image_metadata(&decoder, frame_count)?;

    Ok(ScannedImage {
        info,
        frames,
        icc,
        alpha_associated,
        parallelism,
        primed_still: Mutex::new(None),
    })
}

fn inspect_image_from_slice(data: &[u8], parallelism: usize) -> Result<ScannedImage, String> {
    // Premultiplication is a no-op without alpha and is correct for associated alpha. Only the
    // less common straight-alpha case needs a second metadata-only parse with it disabled.
    let (mut decoder, mut input_offset) =
        read_image_info_from_slice(data, decoder_options(false, true), parallelism)?;
    if decoder.basic_info().animation.is_some() {
        let mut animation_input = data;
        return scan_animated_image(&mut animation_input, parallelism);
    }

    let straight_alpha = decoder
        .basic_info()
        .extra_channels
        .iter()
        .any(|channel| channel.ec_type == ExtraChannel::Alpha && !channel.alpha_associated);
    if straight_alpha {
        (decoder, input_offset) =
            read_image_info_from_slice(data, decoder_options(false, false), parallelism)?;
    }

    finish_still_image(decoder, input_offset, parallelism)
}

fn inspect_image_from_reader<R: Read + Seek>(
    input: &mut BufReader<R>,
    parallelism: usize,
) -> Result<ScannedImage, String> {
    input
        .seek(SeekFrom::Start(0))
        .map_err(|error| error.to_string())?;
    let (mut decoder, mut input_offset) =
        read_image_info_from_reader(input, decoder_options(false, true), parallelism)?;
    if decoder.basic_info().animation.is_some() {
        input
            .seek(SeekFrom::Start(0))
            .map_err(|error| error.to_string())?;
        return scan_animated_image(input, parallelism);
    }

    let straight_alpha = decoder
        .basic_info()
        .extra_channels
        .iter()
        .any(|channel| channel.ec_type == ExtraChannel::Alpha && !channel.alpha_associated);
    if straight_alpha {
        input
            .seek(SeekFrom::Start(0))
            .map_err(|error| error.to_string())?;
        (decoder, input_offset) =
            read_image_info_from_reader(input, decoder_options(false, false), parallelism)?;
    }

    finish_still_image(decoder, input_offset, parallelism)
}

fn configure_output_format(decoder: &mut JxlDecoder<states::WithImageInfo>, channels: u32) {
    let color_type = match channels {
        1 => JxlColorType::Grayscale,
        3 => JxlColorType::Bgr,
        4 => JxlColorType::Bgra,
        _ => unreachable!("validated channel count"),
    };
    decoder.set_pixel_format(JxlPixelFormat {
        color_type,
        color_data_format: Some(JxlDataFormat::U8 { bit_depth: 8 }),
        extra_channel_format: vec![None; decoder.basic_info().extra_channels.len()],
    });
}

fn decode_current_frame(
    mut decoder: JxlDecoder<states::WithFrameInfo>,
    input: &mut impl JxlBitstreamInput,
    output: JxlOutputBuffer<'_>,
    parallelism: usize,
) -> Result<(), String> {
    let mut buffers = [output];
    let mut runner = ParallelRunner::new(parallelism);
    loop {
        match decoder
            .process(input, &mut buffers, runner.as_option())
            .map_err(|error| error.to_string())?
        {
            ProcessingResult::Complete { .. } => return Ok(()),
            ProcessingResult::NeedsMoreInput { fallback, .. } => {
                needs_more_input(input, "decoding pixels")?;
                decoder = fallback;
            }
        }
    }
}

fn prepare_output<'a>(
    image: &ScannedImage,
    output: *mut u8,
    output_length: usize,
    output_stride: usize,
) -> Result<JxlOutputBuffer<'a>, String> {
    if output.is_null() {
        return Err("A null output pointer was passed to jxl-rs".into());
    }
    let height = image.info.height as usize;
    let row_bytes = (image.info.width as usize)
        .checked_mul(image.info.channels as usize)
        .ok_or("JPEG XL row size exceeds addressable memory")?;
    let required = output_stride
        .checked_mul(height.saturating_sub(1))
        .and_then(|size| size.checked_add(row_bytes))
        .ok_or("JPEG XL output size exceeds addressable memory")?;
    if output_stride < row_bytes || output_length < required {
        return Err("JPEG XL output buffer is too small".into());
    }

    // SAFETY: The FFI caller guarantees that output references initialized writable bytes for
    // output_length. The size checks above guarantee that all rows fit in that allocation.
    Ok(unsafe { JxlOutputBuffer::new_from_ptr(output, height, row_bytes, output_stride) })
}

fn take_primed_still(image: &ScannedImage) -> Option<PrimedStillDecoder> {
    image
        .primed_still
        .lock()
        .unwrap_or_else(|poisoned| poisoned.into_inner())
        .take()
}

fn decode_frame_from_slice(
    image: &ScannedImage,
    frame_index: u32,
    data: &[u8],
    output: *mut u8,
    output_length: usize,
    output_stride: usize,
) -> Result<(), String> {
    let frame = image
        .frames
        .get(frame_index as usize)
        .ok_or("Invalid JPEG XL frame index")?;

    if let Some(seek_target) = frame.seek_target {
        let mut input = data;
        let mut decoder = read_image_info(
            &mut input,
            decoder_options(false, image.alpha_associated),
            image.parallelism,
        )?;
        configure_output_format(&mut decoder, image.info.channels);
        decoder.start_new_frame(seek_target);
        let offset = usize::try_from(seek_target.decode_start_file_offset)
            .map_err(|_| "JPEG XL frame offset exceeds addressable memory")?;
        input = data
            .get(offset..)
            .ok_or("JPEG XL frame offset is outside the input")?;
        let frame_decoder = read_frame_header(decoder, &mut input, image.parallelism)?;
        let output_buffer = prepare_output(image, output, output_length, output_stride)?;
        return decode_current_frame(frame_decoder, &mut input, output_buffer, image.parallelism);
    }

    let (mut decoder, mut input) = if let Some(primed) = take_primed_still(image) {
        let offset = usize::try_from(primed.input_offset)
            .map_err(|_| "JPEG XL input offset exceeds addressable memory")?;
        let input = data
            .get(offset..)
            .ok_or("JPEG XL input offset is outside the input")?;
        (primed.decoder, input)
    } else {
        let mut input = data;
        let decoder = read_image_info(
            &mut input,
            decoder_options(false, image.alpha_associated),
            image.parallelism,
        )?;
        (decoder, input)
    };
    configure_output_format(&mut decoder, image.info.channels);
    let frame_decoder = read_frame_header(decoder, &mut input, image.parallelism)?;
    let output_buffer = prepare_output(image, output, output_length, output_stride)?;
    decode_current_frame(frame_decoder, &mut input, output_buffer, image.parallelism)
}

fn decode_frame_from_reader<R: Read + Seek>(
    image: &ScannedImage,
    frame_index: u32,
    input: &mut BufReader<R>,
    output: *mut u8,
    output_length: usize,
    output_stride: usize,
) -> Result<(), String> {
    let frame = image
        .frames
        .get(frame_index as usize)
        .ok_or("Invalid JPEG XL frame index")?;

    let decoder = if let Some(seek_target) = frame.seek_target {
        input
            .seek(SeekFrom::Start(0))
            .map_err(|error| error.to_string())?;
        let mut decoder = read_image_info(
            input,
            decoder_options(false, image.alpha_associated),
            image.parallelism,
        )?;
        configure_output_format(&mut decoder, image.info.channels);
        decoder.start_new_frame(seek_target);
        input
            .seek(SeekFrom::Start(seek_target.decode_start_file_offset))
            .map_err(|error| error.to_string())?;
        decoder
    } else if let Some(primed) = take_primed_still(image) {
        input
            .seek(SeekFrom::Start(primed.input_offset))
            .map_err(|error| error.to_string())?;
        let mut decoder = primed.decoder;
        configure_output_format(&mut decoder, image.info.channels);
        decoder
    } else {
        input
            .seek(SeekFrom::Start(0))
            .map_err(|error| error.to_string())?;
        let mut decoder = read_image_info(
            input,
            decoder_options(false, image.alpha_associated),
            image.parallelism,
        )?;
        configure_output_format(&mut decoder, image.info.channels);
        decoder
    };
    let frame_decoder = read_frame_header(decoder, input, image.parallelism)?;
    let output_buffer = prepare_output(image, output, output_length, output_stride)?;
    decode_current_frame(frame_decoder, input, output_buffer, image.parallelism)
}

fn with_ffi_result<T>(operation: impl FnOnce() -> Result<T, String>) -> Option<T> {
    match catch_unwind(AssertUnwindSafe(operation)) {
        Ok(Ok(value)) => Some(value),
        Ok(Err(error)) => {
            set_error(error);
            None
        }
        Err(_) => {
            set_error("jxl-rs panicked while decoding the image");
            None
        }
    }
}

fn callback_reader(
    stream: *const JxlRsStream,
    capacity: usize,
) -> Result<BufReader<CallbackStream>, String> {
    if stream.is_null() {
        return Err("A null stream pointer was passed to jxl-rs".into());
    }
    // SAFETY: The caller guarantees that stream is readable for this synchronous call.
    let callbacks = unsafe { &*stream };
    if callbacks.read.is_none() || callbacks.seek.is_none() {
        return Err("JPEG XL stream callbacks are incomplete".into());
    }
    Ok(BufReader::with_capacity(
        capacity,
        CallbackStream {
            callbacks: JxlRsStream {
                context: callbacks.context,
                read: callbacks.read,
                seek: callbacks.seek,
            },
        },
    ))
}

#[unsafe(no_mangle)]
pub extern "C" fn jxlrs_version() -> u32 {
    VERSION
}

/// Reads JPEG XL metadata and, for animations, visible frame locations without rendering pixels.
///
/// # Safety
/// `data` must reference `length` readable bytes and `info` must be writable.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn jxlrs_inspect(
    data: *const u8,
    length: usize,
    info: *mut JxlRsImageInfo,
) -> *mut ScannedImage {
    if data.is_null() || info.is_null() {
        set_error("A null pointer was passed to jxlrs_inspect");
        return ptr::null_mut();
    }
    with_ffi_result(|| {
        // SAFETY: The caller guarantees that data references length readable bytes.
        let input = unsafe { slice::from_raw_parts(data, length) };
        let image = inspect_image_from_slice(input, 1)?;
        // SAFETY: The caller guarantees that info is writable.
        unsafe { info.write(image.info) };
        Ok(Box::into_raw(Box::new(image)))
    })
    .unwrap_or(ptr::null_mut())
}

/// Reads JPEG XL metadata with configurable decode parallelism.
///
/// A parallelism value of zero uses the shared pool's automatic limit, while one disables
/// parallel execution.
///
/// # Safety
/// data must reference length readable bytes and info must be writable.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn jxlrs_inspect_with_options(
    data: *const u8,
    length: usize,
    info: *mut JxlRsImageInfo,
    parallelism: u32,
) -> *mut ScannedImage {
    if data.is_null() || info.is_null() {
        set_error("A null pointer was passed to jxlrs_inspect_with_options");
        return ptr::null_mut();
    }
    with_ffi_result(|| {
        // SAFETY: The caller guarantees that data references length readable bytes.
        let input = unsafe { slice::from_raw_parts(data, length) };
        let image = inspect_image_from_slice(input, parallelism as usize)?;
        // SAFETY: The caller guarantees that info is writable.
        unsafe { info.write(image.info) };
        Ok(Box::into_raw(Box::new(image)))
    })
    .unwrap_or(ptr::null_mut())
}

/// Reads JPEG XL metadata through bounded callbacks and scans frame locations only for animations.
///
/// # Safety
/// `stream` and its callbacks must remain valid for this call and `info` must be writable.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn jxlrs_inspect_stream(
    stream: *const JxlRsStream,
    info: *mut JxlRsImageInfo,
) -> *mut ScannedImage {
    if info.is_null() {
        set_error("A null info pointer was passed to jxlrs_inspect_stream");
        return ptr::null_mut();
    }
    with_ffi_result(|| {
        let mut input = callback_reader(stream, METADATA_BUFFER_SIZE)?;
        let image = inspect_image_from_reader(&mut input, 1)?;
        // SAFETY: The caller guarantees that info is writable.
        unsafe { info.write(image.info) };
        Ok(Box::into_raw(Box::new(image)))
    })
    .unwrap_or(ptr::null_mut())
}

/// Reads JPEG XL metadata through bounded callbacks with configurable decode parallelism.
///
/// # Safety
/// stream and its callbacks must remain valid for this call and info must be writable.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn jxlrs_inspect_stream_with_options(
    stream: *const JxlRsStream,
    info: *mut JxlRsImageInfo,
    parallelism: u32,
) -> *mut ScannedImage {
    if info.is_null() {
        set_error("A null info pointer was passed to jxlrs_inspect_stream_with_options");
        return ptr::null_mut();
    }
    with_ffi_result(|| {
        let mut input = callback_reader(stream, METADATA_BUFFER_SIZE)?;
        let image = inspect_image_from_reader(&mut input, parallelism as usize)?;
        // SAFETY: The caller guarantees that info is writable.
        unsafe { info.write(image.info) };
        Ok(Box::into_raw(Box::new(image)))
    })
    .unwrap_or(ptr::null_mut())
}

/// Returns metadata for one visible frame.
///
/// # Safety
/// `handle` must be returned by an inspect function and `info` must be writable.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn jxlrs_get_frame(
    handle: *const ScannedImage,
    index: u32,
    info: *mut JxlRsFrameInfo,
) -> bool {
    if handle.is_null() || info.is_null() {
        return false;
    }
    // SAFETY: The caller guarantees handle and info are valid.
    let image = unsafe { &*handle };
    let Some(frame) = image.frames.get(index as usize) else {
        return false;
    };
    // SAFETY: The caller guarantees info is writable.
    unsafe {
        info.write(JxlRsFrameInfo {
            duration_milliseconds: frame.duration_milliseconds,
        });
    }
    true
}

/// Decodes one visible frame directly into caller-owned memory.
///
/// # Safety
/// All pointers must be valid for their documented lengths for this synchronous call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn jxlrs_decode_frame(
    handle: *const ScannedImage,
    frame_index: u32,
    data: *const u8,
    length: usize,
    output: *mut u8,
    output_length: usize,
    output_stride: usize,
) -> bool {
    if handle.is_null() || data.is_null() {
        set_error("A null pointer was passed to jxlrs_decode_frame");
        return false;
    }
    with_ffi_result(|| {
        // SAFETY: The caller guarantees the handle and input allocation are valid for this call.
        let image = unsafe { &*handle };
        let bytes = unsafe { slice::from_raw_parts(data, length) };
        decode_frame_from_slice(
            image,
            frame_index,
            bytes,
            output,
            output_length,
            output_stride,
        )
    })
    .is_some()
}

/// Decodes one visible frame directly into caller-owned memory using bounded stream callbacks.
///
/// # Safety
/// All pointers and callbacks must remain valid for this synchronous call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn jxlrs_decode_frame_stream(
    handle: *const ScannedImage,
    frame_index: u32,
    stream: *const JxlRsStream,
    output: *mut u8,
    output_length: usize,
    output_stride: usize,
) -> bool {
    if handle.is_null() {
        set_error("A null handle was passed to jxlrs_decode_frame_stream");
        return false;
    }
    with_ffi_result(|| {
        // SAFETY: The caller guarantees the handle remains valid for this call.
        let image = unsafe { &*handle };
        let mut input = callback_reader(stream, STREAM_BUFFER_SIZE)?;
        decode_frame_from_reader(
            image,
            frame_index,
            &mut input,
            output,
            output_length,
            output_stride,
        )
    })
    .is_some()
}

/// Returns a borrowed pointer to the output ICC profile discovered during metadata scanning.
///
/// # Safety
/// `handle` must be returned by an inspect function.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn jxlrs_get_icc(handle: *const ScannedImage) -> *const u8 {
    if handle.is_null() {
        return ptr::null();
    }
    // SAFETY: The caller guarantees handle is valid.
    unsafe { (*handle).icc.as_ptr() }
}

/// Releases an image metadata handle.
///
/// # Safety
/// `handle` must be null or a live pointer returned by an inspect function exactly once.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn jxlrs_destroy(handle: *mut ScannedImage) {
    if !handle.is_null() {
        // SAFETY: The caller guarantees unique ownership of this live handle.
        drop(unsafe { Box::from_raw(handle) });
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn jxlrs_last_error() -> *const c_char {
    LAST_ERROR.with(|slot| slot.borrow().as_ptr())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::atomic::{AtomicUsize, Ordering};

    const BASIC_JXL: &[u8] = &[
        255, 10, 0, 144, 1, 0, 18, 136, 2, 0, 212, 0, 85, 15, 0, 0, 168, 80, 25, 101, 220, 224,
        229, 92, 207, 151, 31, 58, 44, 166, 109, 92, 103, 104, 171, 109, 11, 75, 18, 69, 198, 177,
        73, 58, 129, 67, 146, 88, 4, 54, 46, 152, 7, 24, 0, 134, 153, 3, 39, 51, 80, 228, 74, 18,
        0,
    ];

    #[test]
    fn scans_and_decodes_basic_image() {
        let image = inspect_image_from_slice(BASIC_JXL, 1).expect("scan");
        assert_eq!(image.info.width, 1);
        assert_eq!(image.info.height, 1);
        assert_eq!(image.info.frame_count, 1);

        assert!(image.primed_still.lock().expect("lock").is_some());

        let mut pixels = vec![0_u8; image.info.channels as usize];
        decode_frame_from_slice(
            &image,
            0,
            BASIC_JXL,
            pixels.as_mut_ptr(),
            pixels.len(),
            pixels.len(),
        )
        .expect("decode");
        assert!(image.primed_still.lock().expect("lock").is_none());

        let mut repeated = vec![0_u8; image.info.channels as usize];
        decode_frame_from_slice(
            &image,
            0,
            BASIC_JXL,
            repeated.as_mut_ptr(),
            repeated.len(),
            repeated.len(),
        )
        .expect("fallback decode");
        assert_eq!(pixels, repeated);
    }

    #[test]
    fn bounds_still_metadata_input() {
        let mut padded = BASIC_JXL.to_vec();
        padded.resize(METADATA_BUFFER_SIZE * 3, 0);

        let image = inspect_image_from_slice(&padded, 1).expect("slice scan");
        let primed_offset = image
            .primed_still
            .lock()
            .expect("lock")
            .as_ref()
            .expect("primed decoder")
            .input_offset;
        assert!(primed_offset <= METADATA_BUFFER_SIZE as u64);
        assert!(primed_offset < padded.len() as u64);

        let cursor = io::Cursor::new(padded);
        let mut reader = BufReader::with_capacity(METADATA_BUFFER_SIZE, cursor);
        let image = inspect_image_from_reader(&mut reader, 1).expect("reader scan");
        let primed_offset = image
            .primed_still
            .lock()
            .expect("lock")
            .as_ref()
            .expect("primed decoder")
            .input_offset;
        assert!(primed_offset <= METADATA_BUFFER_SIZE as u64);
    }

    #[test]
    fn rejects_truncated_input() {
        assert!(inspect_image_from_slice(&BASIC_JXL[..2], 1).is_err());
    }

    #[test]
    fn parallel_runner_executes_each_task_once() {
        let visits: Vec<_> = (0..257).map(|_| AtomicUsize::new(0)).collect();
        ParallelRunner::new(4)
            .run(visits.len(), &|index| {
                visits[index].fetch_add(1, Ordering::Relaxed);
                Ok(())
            })
            .expect("parallel run");

        assert!(
            visits
                .iter()
                .all(|visit| visit.load(Ordering::Relaxed) == 1)
        );
    }
}
