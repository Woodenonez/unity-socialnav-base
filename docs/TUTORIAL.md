## Project Structure
The project is organized into the following directories:
- `ws_py`: Contains the Python workspace for the project
- `ws_ros`: Contains the ROS2 package
- `ws_unity`: Contains the Unity project

## TODO
- [ ] Support docker for ROS2

## References
Unity:
- [Unity Robotics](https://github.com/Unity-Technologies/Unity-Robotics-Hub)
- [ROS TCP Connector](https://github.com/Unity-Technologies/ROS-TCP-Connector)
- [ROS TCP Endpoint](https://github.com/Unity-Technologies/ROS-TCP-Endpoint)
- [URDF Importer](https://github.com/Unity-Technologies/URDF-Importer)

ROS2:
- []()

## Quick Start
### Prerequisites
- Ubuntu 24.04
- ROS2 (Jazzy)
- Unity (6000.3)
Make sure to install the necessary dependencies:
```bash
sudo apt update
sudo apt install ros-jazzy-navigation2 ros-jazzy-nav2-bringup ros-jazzy-slam-toolbox ros-jazzy-rqt-tf-tree ros-jazzy-turtlebot3-description ros-jazzy-teleop-twist-keyboard
```

### 1. **Build connection**
Open the Unity project `ws_unity`, from the menu, select `Window > Package Manager`, then click the `+` button and select `Install package from git URL`, enter the following URL:
```
https://github.com/Unity-Technologies/ROS-TCP-Connector.git?path=/com.unity.robotics.ros-tcp-connector
```
After completing the installation, a new menu item `Robotics` will appear. In the `Robotics > ROS Settings`, set protocol to `ROS2`. The IP address should be `127.0.0.1` and the port should be `10000` (for local connection). 

Add Empty GameObject and name it to `ROSConnection`, add component named `ROS Connection`. Check settings such as IP address and port number. After clicking play, ROS 2 connection mark should be visible in the top left corner of the Game view.

On the other hand, go to `ws_ros2`, clone the ROS TCP Endpoint repository, and build the ROS2 package:
```bash
cd src
git clone -b main-ros2 https://github.com/Unity-Technologies/ROS-TCP-Endpoint.git
cd ..
colcon build
```
Test the ROS2 package by running the following command:
```bash
source install/setup.bash
ros2 run ros_tcp_endpoint default_server_endpoint --ros-args -p ROS_IP:=127.0.0.1
```
It should show a message indicating that the connection is established.