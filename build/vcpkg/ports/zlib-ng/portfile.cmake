vcpkg_from_github(
    OUT_SOURCE_PATH SOURCE_PATH
    REPO zlib-ng/zlib-ng
    REF "${VERSION}"
    SHA512 59ef586c09b9a63788475abfd6dd59ed602316b38f543f801bea802ff8bec8b55a89bee90375b8bbffa3bdebc7d92a00903f4b7c94cdc1a53a36e2e1fd71d13a
    HEAD_REF develop
)

vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}"
    OPTIONS
        -DZLIB_COMPAT=ON
        -DZLIB_ENABLE_TESTS=OFF
        -DZLIBNG_ENABLE_TESTS=OFF
        -DWITH_GZFILEOP=OFF
        -DWITH_NEW_STRATEGIES=ON
        -DWITH_GTEST=OFF
    OPTIONS_RELEASE
        -DWITH_OPTIM=ON
        -DFORCE_SSE2=ON
    MAYBE_UNUSED_VARIABLES
        FORCE_SSE2
)
vcpkg_cmake_install()
vcpkg_copy_pdbs()
vcpkg_fixup_pkgconfig()
vcpkg_cmake_config_fixup(CONFIG_PATH lib/cmake)

file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/share"
                    "${CURRENT_PACKAGES_DIR}/debug/include"
)
vcpkg_install_copyright(FILE_LIST "${SOURCE_PATH}/LICENSE.md")
