vcpkg_from_github(
    OUT_SOURCE_PATH SOURCE_PATH
    REPO  strukturag/libheif 
    REF "v${VERSION}"
    SHA512 ef32fced3a66d888caf2202b55bc4c81094045abfd2806216bbf0c359a30663c500ed5e33a9cef316bcfd933498e87753e9af6b3c179e84c370efd62900493c0
    HEAD_REF master
    PATCHES
        dav1d-settings.patch
)

vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}"
    OPTIONS
        -DENABLE_PLUGIN_LOADING=OFF
        -DENABLE_PARALLEL_TILE_DECODING=OFF
        -DWITH_EXAMPLES=OFF
        -DWITH_AOM_DECODER=OFF
        -DWITH_AOM_ENCODER=OFF
        -DWITH_X265=OFF
        -DWITH_RAV1E=OFF
        -DWITH_SvtEnc=OFF
)
vcpkg_cmake_install()
vcpkg_copy_pdbs()

vcpkg_cmake_config_fixup(CONFIG_PATH lib/cmake/libheif/)
# libheif's pc file assumes libstdc++, which isn't always true.
vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/lib/pkgconfig/libheif.pc" " -lstdc++" "")
if(NOT VCPKG_BUILD_TYPE)
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/debug/lib/pkgconfig/libheif.pc" " -lstdc++" "")
endif()
vcpkg_fixup_pkgconfig()

if (VCPKG_LIBRARY_LINKAGE STREQUAL "dynamic")
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/include/libheif/heif.h" "!defined(LIBHEIF_STATIC_BUILD)" "1")
else()
    vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/include/libheif/heif.h" "!defined(LIBHEIF_STATIC_BUILD)" "0")
endif()

file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/include")

vcpkg_replace_string("${CURRENT_PACKAGES_DIR}/include/libheif/heif_version.h" "#define LIBHEIF_PLUGIN_DIRECTORY \"${CURRENT_PACKAGES_DIR}/lib/libheif\"" "")

file(INSTALL "${SOURCE_PATH}/COPYING" DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}" RENAME copyright)
