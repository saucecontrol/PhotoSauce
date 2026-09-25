set(LIBPNG_APNG_PATCH_PATH "")
if ("apng" IN_LIST FEATURES)
    if(VCPKG_HOST_IS_WINDOWS)
        vcpkg_acquire_msys(MSYS_ROOT PACKAGES gawk gzip NO_DEFAULT_PACKAGES)
        vcpkg_add_to_path("${MSYS_ROOT}/usr/bin")
    endif()

    set(LIBPNG_APNG_PATCH_NAME "libpng-${VERSION}-apng.patch")
    vcpkg_download_distfile(LIBPNG_APNG_PATCH_ARCHIVE
        URLS "https://downloads.sourceforge.net/project/libpng-apng/libpng16/${VERSION}/${LIBPNG_APNG_PATCH_NAME}.gz"
        FILENAME "${LIBPNG_APNG_PATCH_NAME}.gz"
        SHA512 95a6f5bb7148b5c48dccd73811d7bcf9752a631a7bb4f4856670a7da12a7159581ac1bce1749318343794e0f5cb86972711ba2ec0f523c168f0991fa940687d5
    )
    set(LIBPNG_APNG_PATCH_PATH "${CURRENT_BUILDTREES_DIR}/src/${LIBPNG_APNG_PATCH_NAME}")
    if (NOT EXISTS "${LIBPNG_APNG_PATCH_PATH}")
        file(INSTALL "${LIBPNG_APNG_PATCH_ARCHIVE}" DESTINATION "${CURRENT_BUILDTREES_DIR}/src")
        vcpkg_execute_required_process(
            COMMAND gzip -d "${LIBPNG_APNG_PATCH_NAME}.gz"
            WORKING_DIRECTORY "${CURRENT_BUILDTREES_DIR}/src"
            ALLOW_IN_DOWNLOAD_MODE
            LOGNAME extract-patch.log
        )
    endif()
endif()

vcpkg_from_github(
    OUT_SOURCE_PATH SOURCE_PATH
    REPO pnggroup/libpng
    REF v${VERSION}
    SHA512 65f54d805e1f7c46a5fc335b984e4cbd4f934e0f02fbf6673c13800b49a4c11fbeb4098eebfb33079527a56c3d933e97631f91ab68dbb31442982784f9241ace
    HEAD_REF libpng16
    PATCHES
        "${LIBPNG_APNG_PATCH_PATH}"
        pspng-customize-build.patch
        pspng-customize-code.patch
)

file(COPY "${CURRENT_PORT_DIR}/pngusr.h" "${CURRENT_PORT_DIR}/pspng.h"
          "${CURRENT_PORT_DIR}/pspng.c" "${CURRENT_PORT_DIR}/pspng.ver" DESTINATION "${SOURCE_PATH}")

set(VCPKG_C_FLAGS -DPNG_USER_CONFIG)
set(VCPKG_CXX_FLAGS -DPNG_USER_CONFIG)

vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}"
    OPTIONS
        -DPNG_STATIC=OFF
        -DPNG_SHARED=OFF
        -DPNG_FRAMEWORK=OFF
        -DPNG_TESTS=OFF
        -DSKIP_INSTALL_ALL=ON
    MAYBE_UNUSED_VARIABLES
        PNG_ARM_NEON
)
vcpkg_cmake_install()
vcpkg_copy_pdbs()

file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/share"
                    "${CURRENT_PACKAGES_DIR}/debug/include"
)
vcpkg_install_copyright(FILE_LIST "${SOURCE_PATH}/LICENSE")
