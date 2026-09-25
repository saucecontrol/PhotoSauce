vcpkg_from_github(
    OUT_SOURCE_PATH SOURCE_PATH
    REPO libjpeg-turbo/libjpeg-turbo
    REF "${VERSION}"
    SHA512 1ce063e9e126d55019385da3a6ff4521a9a3958edeab15e2465ae3435026dac6e598277eea066173ada47da37159d0f7812b2227869c10dd141dcfe2ddeab720
    HEAD_REF master
    PATCHES
        add-options-for-docs-headers.patch
        # workaround for vcpkg bug see #5697 on github for more information
        workaround_cmake_system_processor.patch
        psjpeg-customize-build.patch
        psjpeg-customize-code.patch
)

file(COPY ${CURRENT_PORT_DIR}/psjpeg.h ${CURRENT_PORT_DIR}/psjpeg.c ${CURRENT_PORT_DIR}/psjpeg.ver DESTINATION ${SOURCE_PATH}/src)

if(VCPKG_TARGET_ARCHITECTURE STREQUAL "wasm32")
    set(LIBJPEGTURBO_SIMD -DWITH_SIMD=OFF)
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
