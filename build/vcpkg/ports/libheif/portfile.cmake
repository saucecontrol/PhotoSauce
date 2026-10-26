vcpkg_from_github(
    OUT_SOURCE_PATH SOURCE_PATH
    REPO strukturag/libheif
    REF "v${VERSION}"
    SHA512 e3b59c0ab6da9a86273d3f6de693d5d1fc955c84d97ca5514c58c62b6ff8ece44e81e215a126d6dbb4248b1e344f60251443c520058033fd71a7282263492180
    HEAD_REF master
    PATCHES
        cxx-linkage-pkgconfig.diff
        find-modules.diff
        symbol-exports.diff
        ps-customize-build.patch
)

vcpkg_check_features(OUT_FEATURE_OPTIONS FEATURE_OPTIONS
    FEATURES
        aom         WITH_AOM_DECODER
        aom         WITH_AOM_ENCODER
        aom         VCPKG_LOCK_FIND_PACKAGE_AOM
        dav1d       WITH_DAV1D
        dav1d       VCPKG_LOCK_FIND_PACKAGE_DAV1D
        gdk-pixbuf  WITH_GDK_PIXBUF
        hevc        WITH_X265
        hevc        VCPKG_LOCK_FIND_PACKAGE_X265
        iso23001-17 WITH_UNCOMPRESSED_CODEC
        kvazaar     WITH_KVAZAAR
        kvazaar     VCPKG_LOCK_FIND_PACKAGE_kvazaar
        header-compression WITH_HEADER_COMPRESSION
        jpeg        WITH_JPEG_DECODER
        jpeg        WITH_JPEG_ENCODER
        jpeg        VCPKG_LOCK_FIND_PACKAGE_JPEG
        libde265    WITH_LIBDE265
        libde265    VCPKG_LOCK_FIND_PACKAGE_LIBDE265
        openjpeg    WITH_OpenJPEG_DECODER
        openjpeg    WITH_OpenJPEG_ENCODER
        openjpeg    VCPKG_LOCK_FIND_PACKAGE_OpenJPEG
        uvg266      WITH_UVG266
        uvg266      VCPKG_LOCK_FIND_PACKAGE_UVG266
        vvdec       WITH_VVDEC
        vvdec       VCPKG_LOCK_FIND_PACKAGE_vvdec
        vvenc       WITH_VVENC
        vvenc       VCPKG_LOCK_FIND_PACKAGE_vvenc
        x264        WITH_X264
        h264-decoder WITH_OpenH264_DECODER
        h264-decoder VCPKG_LOCK_FIND_PACKAGE_OpenH264
)

if(WITH_UNCOMPRESSED_CODEC OR WITH_HEADER_COMPRESSION)
    set(VCPKG_LOCK_FIND_PACKAGE_ZLIB ON)
    set(VCPKG_LOCK_FIND_PACKAGE_Brotli ON)
else()
    set(VCPKG_LOCK_FIND_PACKAGE_ZLIB OFF)
    set(VCPKG_LOCK_FIND_PACKAGE_Brotli OFF)
endif()

vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}"
    OPTIONS
        -DBUILD_TESTING=OFF
        -DBUILD_DOCUMENTATION=OFF
        -DCMAKE_COMPILE_WARNING_AS_ERROR=OFF
        -DPLUGIN_DIRECTORY=  # empty
        -DWITH_EXAMPLES=OFF
        -DWITH_EXAMPLE_HEIF_THUMB=OFF
        -DWITH_EXAMPLE_HEIF_VIEW=OFF
        -DWITH_OpenH264_ENCODER=OFF
        -DWITH_SvtEnc=OFF
        -DWITH_RAV1E=OFF
        -DWITH_FFMPEG_DECODER=OFF
        -DWITH_OPENJPH_DECODER=OFF
        -DWITH_OPENJPH_ENCODER=OFF
        -DWITH_WEBCODECS=OFF
        -DWITH_LIBSHARPYUV=OFF
        -DENABLE_PLUGIN_LOADING=OFF
        -DENABLE_PARALLEL_TILE_DECODING=OFF
        ${FEATURE_OPTIONS}
    MAYBE_UNUSED_VARIABLES
        # Discovery locks are consumed only when the corresponding codec or compression feature is enabled.
        VCPKG_LOCK_FIND_PACKAGE_AOM
        VCPKG_LOCK_FIND_PACKAGE_Brotli
        VCPKG_LOCK_FIND_PACKAGE_DAV1D
        VCPKG_LOCK_FIND_PACKAGE_JPEG
        VCPKG_LOCK_FIND_PACKAGE_kvazaar
        VCPKG_LOCK_FIND_PACKAGE_LIBDE265
        VCPKG_LOCK_FIND_PACKAGE_OpenH264
        VCPKG_LOCK_FIND_PACKAGE_OpenJPEG
        VCPKG_LOCK_FIND_PACKAGE_UVG266
        VCPKG_LOCK_FIND_PACKAGE_vvdec
        VCPKG_LOCK_FIND_PACKAGE_vvenc
        VCPKG_LOCK_FIND_PACKAGE_X265
        VCPKG_LOCK_FIND_PACKAGE_ZLIB
)
vcpkg_cmake_install()
vcpkg_copy_pdbs()
vcpkg_cmake_config_fixup(CONFIG_PATH "lib/cmake/libheif")
vcpkg_fixup_pkgconfig()

if (VCPKG_LIBRARY_LINKAGE STREQUAL "dynamic")
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/include/libheif/heif_export.h" "!defined(LIBHEIF_STATIC_BUILD)" "1")
else()
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/include/libheif/heif_export.h" "!defined(LIBHEIF_STATIC_BUILD)" "0")
endif()

file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/include")
file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/share")
file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/lib/libheif" "${CURRENT_PACKAGES_DIR}/debug/lib/libheif")

vcpkg_install_copyright(FILE_LIST "${SOURCE_PATH}/COPYING")
