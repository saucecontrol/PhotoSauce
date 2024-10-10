vcpkg_from_github(
    OUT_SOURCE_PATH SOURCE_PATH
    REPO  strukturag/libheif 
    REF "v${VERSION}"
    SHA512 0fcb6340694d5f30a355a0e1224bdbcb35d898594739ffd767cc882842887011a418aa67df08b8cdccc06fa2e477768de90704c8d6f5a827f6878252a13c7734
    HEAD_REF master
    PATCHES
        dav1d-settings.patch
        ps-customize-build.patch
)

vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}"
    OPTIONS
        -DENABLE_PLUGIN_LOADING=OFF
        -DENABLE_MULTITHREADING_SUPPORT=OFF
        -DENABLE_PARALLEL_TILE_DECODING=OFF
        -DBUILD_TESTING=OFF
        -DWITH_EXAMPLES=OFF
        -DWITH_GDK_PIXBUF=OFF
        -DWITH_HEADER_COMPRESSION=OFF
        -DWITH_LIBDE265=ON
        -DWITH_X265=OFF
        -DWITH_KVAZAAR=OFF
        -DWITH_UVG266=OFF
        -DWITH_VVDEC=OFF
        -DWITH_VVENC=OFF
        -DWITH_DAV1D=ON
        -DWITH_AOM_DECODER=OFF
        -DWITH_AOM_ENCODER=OFF
        -DWITH_SvtEnc=OFF
        -DWITH_RAV1E=OFF
        -DWITH_JPEG_DECODER=OFF
        -DWITH_JPEG_ENCODER=OFF
        -DWITH_OpenJPEG_DECODER=OFF
        -DWITH_OpenJPEG_ENCODER=OFF
        -DWITH_FFMPEG_DECODER=OFF
        -DWITH_OPENJPH_DECODER=OFF
        -DWITH_OPENJPH_ENCODER=OFF
        -DWITH_UNCOMPRESSED_CODEC=OFF
        -DWITH_LIBSHARPYUV=OFF
)
vcpkg_cmake_install()
vcpkg_copy_pdbs()

vcpkg_cmake_config_fixup(CONFIG_PATH lib/cmake/libheif/)
# libheif's pc file assumes libstdc++, which isn't always true.
vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/lib/pkgconfig/libheif.pc" " -lstdc++" "" IGNORE_UNCHANGED)
if(NOT VCPKG_BUILD_TYPE)
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/debug/lib/pkgconfig/libheif.pc" " -lstdc++" "" IGNORE_UNCHANGED)
endif()
vcpkg_fixup_pkgconfig()

if (VCPKG_LIBRARY_LINKAGE STREQUAL "dynamic")
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/include/libheif/heif.h" "defined(_MSC_VER) && !defined(LIBHEIF_STATIC_BUILD)" "defined(_WIN32)")
else()
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/include/libheif/heif.h" "defined(_MSC_VER) && !defined(LIBHEIF_STATIC_BUILD)" "0")
endif()
vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/include/libheif/heif.h" "#ifdef LIBHEIF_EXPORTS" "#if 0")

file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/include")
file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/share")
file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/lib/libheif" "${CURRENT_PACKAGES_DIR}/debug/lib/libheif")

vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/include/libheif/heif_version.h" "#define LIBHEIF_PLUGIN_DIRECTORY \"${CURRENT_PACKAGES_DIR}/lib/libheif\"" "")

vcpkg_install_copyright(FILE_LIST "${SOURCE_PATH}/COPYING")
