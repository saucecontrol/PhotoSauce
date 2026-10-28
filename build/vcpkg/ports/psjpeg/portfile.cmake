vcpkg_from_github(
    OUT_SOURCE_PATH SOURCE_PATH
    REPO libjpeg-turbo/libjpeg-turbo
    REF "${VERSION}"
    SHA512 680b97652a51846e1965e70b5498f74ebf9e6eaa31c122546ba7b8eff9fa6b6eb85972802970e94cce4f539518ad421395eaffe121bce7ad7b8f23a9ab9731ef
    HEAD_REF master
    PATCHES
        add-options-for-docs-headers.patch
        psjpeg-customize-build.patch
        psjpeg-customize-code.patch
)

file(COPY ${CURRENT_PORT_DIR}/psjpeg.h ${CURRENT_PORT_DIR}/psjpeg.c ${CURRENT_PORT_DIR}/psjpeg.ver DESTINATION ${SOURCE_PATH}/src)

if(VCPKG_TARGET_ARCHITECTURE STREQUAL "wasm32")
    set(LIBJPEGTURBO_SIMD -DWITH_SIMD=OFF)
elseif(VCPKG_TARGET_ARCHITECTURE STREQUAL "arm64")
    set(LIBJPEGTURBO_SIMD -DWITH_SIMD=ON -DNEON_INTRINSICS=ON)
else()
    set(LIBJPEGTURBO_SIMD -DWITH_SIMD=ON)
    vcpkg_find_acquire_program(NASM)
    get_filename_component(NASM_EXE_PATH ${NASM} DIRECTORY)
    set(ENV{PATH} "$ENV{PATH};${NASM_EXE_PATH}")
endif()

vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}"
    OPTIONS
        -DENABLE_STATIC=OFF
        -DENABLE_SHARED=OFF
        -DREQUIRE_SIMD=ON
        -DWITH_ARITH_DEC=OFF
        -DWITH_ARITH_ENC=OFF
        -DWITH_TURBOJPEG=OFF
        -DWITH_TOOLS=OFF
        ${LIBJPEGTURBO_SIMD}
    MAYBE_UNUSED_VARIABLES
        NEON_INTRINSICS
)

vcpkg_cmake_install()
vcpkg_copy_pdbs()

file(REMOVE_RECURSE
     "${CURRENT_PACKAGES_DIR}/debug/share"
     "${CURRENT_PACKAGES_DIR}/debug/include"
     "${CURRENT_PACKAGES_DIR}/share/man"
     "${CURRENT_PACKAGES_DIR}/share/doc"
)

vcpkg_install_copyright(FILE_LIST "${SOURCE_PATH}/LICENSE.md")
