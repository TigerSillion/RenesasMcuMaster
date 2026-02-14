#include "protocol/RForgeBinaryParser.h"
#include <gtest/gtest.h>
TEST(RForgeParserTest, ParseSinglePingFrame) {
    rf::RForgeBinaryParser parser;
    const QByteArray payload("abc");
    const QByteArray packet = parser.buildCommand(rf::CommandId::Ping, payload);
    parser.feed(packet);
    rf::Frame frame;
    ASSERT_TRUE(parser.tryPopFrame(frame));
    EXPECT_EQ(frame.cmd, rf::CommandId::Ping);
    EXPECT_EQ(frame.payload, payload);
}
TEST(RForgeParserTest, RecoverFromCrcError) {
    rf::RForgeBinaryParser parser;
    QByteArray packet = parser.buildCommand(rf::CommandId::Ping, QByteArray("x"));
    packet[packet.size() - 1] = static_cast<char>(packet[packet.size() - 1] ^ 0xFF);
    parser.feed(packet);
    rf::Frame frame;
    EXPECT_FALSE(parser.tryPopFrame(frame));
    EXPECT_GT(parser.crcErrorCount(), 0);
}
