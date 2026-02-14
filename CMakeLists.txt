# Legacy Build Notice
# This CMake file is retained for historical Qt skeleton only.
# Active development and CI entrypoint: RenesasForge.slnx (src-dotnet/)
cmake_minimum_required(VERSION 3.21)
project(RenesasForgeMVP VERSION 0.1.0 LANGUAGES CXX C)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)
set(CMAKE_AUTOUIC ON)
find_package(Qt6 6.5 REQUIRED COMPONENTS Core Gui Qml Quick SerialPort Test)
add_library(rf_core
    src/common/types.h
    src/transport/ITransport.h
    src/transport/SerialTransport.h
    src/transport/SerialTransport.cpp
    src/protocol/IProtocolParser.h
    src/protocol/Crc16Ccitt.h
    src/protocol/Crc16Ccitt.cpp
    src/protocol/RForgeBinaryParser.h
    src/protocol/RForgeBinaryParser.cpp
    src/protocol/VofaCompatibleParser.h
    src/protocol/VofaCompatibleParser.cpp
    src/protocol/ParserManager.h
    src/protocol/ParserManager.cpp
    src/core/DataEngine.h
    src/core/DataEngine.cpp
    src/core/VarEngine.h
    src/core/VarEngine.cpp
    src/core/RecordEngine.h
    src/core/RecordEngine.cpp
    src/core/ConnectionManager.h
    src/core/ConnectionManager.cpp
    src/viewmodel/ConnectionVM.h
    src/viewmodel/ConnectionVM.cpp
    src/viewmodel/WaveformVM.h
    src/viewmodel/WaveformVM.cpp
    src/viewmodel/VariableVM.h
    src/viewmodel/VariableVM.cpp
    src/viewmodel/RecorderVM.h
    src/viewmodel/RecorderVM.cpp
)
target_include_directories(rf_core PUBLIC src)
target_link_libraries(rf_core PUBLIC Qt6::Core Qt6::SerialPort)
qt_add_executable(renesas_forge_mvp src/app/main.cpp)
target_link_libraries(renesas_forge_mvp PRIVATE rf_core Qt6::Core Qt6::Gui Qt6::Qml Qt6::Quick)
qt_add_qml_module(renesas_forge_mvp URI RenesasForge VERSION 1.0 QML_FILES src/ui/qml/Main.qml)
enable_testing()
find_package(GTest QUIET)
if(GTest_FOUND)
  add_executable(test_rforge_parser tests/unit/test_rforge_parser.cpp)
  target_link_libraries(test_rforge_parser PRIVATE rf_core GTest::gtest GTest::gtest_main Qt6::Core)
  add_test(NAME test_rforge_parser COMMAND test_rforge_parser)
endif()
add_executable(test_connection_vm tests/ui/test_connection_vm.cpp)
target_link_libraries(test_connection_vm PRIVATE rf_core Qt6::Core Qt6::Test)
add_test(NAME test_connection_vm COMMAND test_connection_vm)

