vcpkg_from_github(
    OUT_SOURCE_PATH SOURCE_PATH
    REPO zlib-ng/zlib-ng
    REF "${VERSION}"
    SHA512 6374a673852935468f23a74ce56db6afb27af3923fab2a0ba182a7a95acea93ff2c979612db6cd355d8bd1a44ebb0779900be78bdd05a89374b339d29339647c
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
