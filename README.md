# Social Navigation with ROS2 and Unity (Trial)

## Project Structure
There are three main parts of this project:
1. Python Workspace: Contains the algorithms for the robot and the agent.
2. ROS2 Workspace: Contains the ROS2 nodes and packages.
3. Unity Workspace: Contains the Unity scene and scripts.

## Quick Start
### Prepare Unity Environment
1. Download and install Unity Hub. In this hub, select `Add project from disk` and select the `ws_unity` folder in this repository. An automatic hint should pop up to ask you to install the Unity version (6000.3.10f1) used in this project.
2. After entering the project, select `Window > Package Manager`, then click the `+` button and select `Install package from git URL`, enter the following URL: 
```
https://github.com/Unity-Technologies/ROS-TCP-Connector.git?path=/com.unity.robotics.ros-tcp-connector
```
After completing the installation, a new menu item `Robotics` will appear. In the `Robotics > ROS Settings`, set protocol to `ROS2`. The IP address should be `127.0.0.1` and the port should be `10000` (for local connection). 

Add an Empty GameObject and name it `ROSConnection`, add a component named `ROS Connection`. Check settings such as IP address and port number. After clicking play, the ROS 2 connection mark should be visible in the top left corner of the Game view.
3. (Optional) Similar to the previous step, import the URDF package into the Unity project. In the `Package Manager`, click the `+` button and select `Install package from git URL`, enter the following URL:
```
https://github.com/Unity-Technologies/URDF-Importer.git?path=/com.unity.robotics.urdf-importer#v0.5.2
```
4. The pedestrian uses preset prefabs, which require downloading the [Starter Assets - ThirdPerson | URP](https://assetstore.unity.com/packages/p/starter-assets-thirdperson-urp-196526) from the Unity Asset Store. After importing it into the project, the prefab `Ped` is ready to use under' Assets> Agents'.
5. Go to `Assets > Scripts > Agents` and drag `PedestrianAgent.cs` and `SendPedDestination.cs` to the Ped object. Figure out how they work.
### Prepare ROS2 Environment
1. Install ROS2 (Jazzy) and the necessary dependencies:
```bash
sudo apt update
sudo apt install ros-jazzy-navigation2 ros-jazzy-nav2-bringup ros-jazzy-slam-toolbox ros-jazzy-rqt-tf-tree ros-jazzy-turtlebot3-description ros-jazzy-teleop-twist-keyboard
```
2. Go to `ws_ros`, clone the ROS TCP Endpoint repository (if it is not there), and build the ROS2 package:
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

## Scenes
Currently, there are two scenes: Sample and Test scenes. In the sample scene, all supported robots are added, with a pedestrian agent. In the test scene, a lab floor is added.
