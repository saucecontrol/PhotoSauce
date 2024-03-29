if(VCPKG_TARGET_ARCHITECTURE STREQUAL "arm64")
  set(CMAKE_CXX_COMPILER "aarch64-linux-gnu-g++-10")
  set(CMAKE_C_COMPILER "aarch64-linux-gnu-gcc-10")
  set(CMAKE_ASM_COMPILER "aarch64-linux-gnu-gcc-10")
else()
  set(CMAKE_CXX_COMPILER "g++-10")
  set(CMAKE_C_COMPILER "gcc-10")
  set(CMAKE_ASM_COMPILER "gcc-10")
endif()

include(${CMAKE_CURRENT_LIST_DIR}/../../../modules/vcpkg/scripts/toolchains/linux.cmake)
