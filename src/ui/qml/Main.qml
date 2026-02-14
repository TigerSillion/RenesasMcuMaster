import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
ApplicationWindow {
    width: 1200
    height: 760
    visible: true
    title: "RenesasForge MVP"
    header: ToolBar {
        RowLayout {
            anchors.fill: parent
            spacing: 8
            Label { text: "Status: " + connectionVM.status }
            Button { text: "Connect COM3@921600"; onClicked: connectionVM.connectPort("COM3", 921600) }
            Button { text: "Disconnect"; onClicked: connectionVM.disconnectPort() }
            Label { text: "Frames: " + waveformVM.frameCount }
            Label { text: "Vars: " + variableVM.variableCount }
        }
    }
    Rectangle {
        anchors.fill: parent
        color: "#0f172a"
        Column {
            anchors.centerIn: parent
            spacing: 10
            Label { text: "Waveform panel placeholder"; color: "#e2e8f0" }
            Label { text: "Rendering + LOD engine will be integrated in Phase C"; color: "#94a3b8" }
        }
    }
}
