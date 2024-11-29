vcpkg_from_github(
    OUT_SOURCE_PATH SOURCE_PATH
    REPO strukturag/libheif
    REF "v${VERSION}"
    SHA512 ff6aedef3e848efed8dd274cb8bfd84fc9d08591ce1d4cba7a88f11625dc875eb5f53d7d14bab01693859f1518ae54f958b66ea7d86f3a1791eb07c05a3b0358
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
        -DENABLE_EXPERIMENTAL_FEATURES=OFF
        -DBUILD_TESTING=OFF
        -DWITH_EXAMPLES=OFF
        -DWITH_GDK_PIXBUF=OFF
        -DWITH_AOM_DECODER=OFF
        -DWITH_AOM_ENCODER=OFF
        -DWITH_DAV1D=ON
        -DWITH_LIBDE265=ON
        -DWITH_RAV1E=OFF
        -DWITH_SvtEnc=OFF
        -DWITH_X265=OFF
        -DWITH_JPEG_DECODER=OFF
        -DWITH_JPEG_ENCODER=OFF
        -DWITH_UNCOMPRESSED_CODEC=OFF
        -DWITH_KVAZAAR=OFF
        -DWITH_OpenJPEG_DECODER=OFF
        -DWITH_OpenJPEG_ENCODER=OFF
        -DWITH_OPENJPH_DECODER=OFF
        -DWITH_OPENJPH_ENCODER=OFF
        -DWITH_FFMPEG_DECODER=OFF
        -DWITH_OpenH264_DECODER=OFF
        -DWITH_OpenH264_ENCODER=OFF
        -DWITH_UVG266=OFF
        -DWITH_VVDEC=OFF
        -DWITH_VVENC=OFF
        -DWITH_HEADER_COMPRESSION=OFF
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
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/include/libheif/heif.h" "!defined(LIBHEIF_STATIC_BUILD)" "1")
else()
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/include/libheif/heif.h" "!defined(LIBHEIF_STATIC_BUILD)" "0")
endif()
vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/include/libheif/heif.h" "#ifdef LIBHEIF_EXPORTS" "#if 0")

file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/include")
file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/share")
file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/lib/libheif" "${CURRENT_PACKAGES_DIR}/debug/lib/libheif")

vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/include/libheif/heif_version.h" "#define LIBHEIF_PLUGIN_DIRECTORY \"${CURRENT_PACKAGES_DIR}/lib/libheif\"" "")

vcpkg_install_copyright(FILE_LIST "${SOURCE_PATH}/COPYING")
