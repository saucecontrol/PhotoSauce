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
        SHA512 ea89018a02ed171b82af9644ec2ff658c8a288e99b5470c7a3fd142c6fa95bbe19cd34c4fae654bc8783b41c3eb3b2d15b486bda3b0307ec3090e99f34465e20
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
    SHA512 c023bc7dcf3d0ea045a63204f2266b2c53b601b99d7c5f5a7b547bc9a48b205a277f699eefa47f136483f495175b226527097cd447d6b0fbceb029eb43638f63
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
