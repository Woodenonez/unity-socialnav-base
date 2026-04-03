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

On the other hand, go to `ws_ros`, clone the ROS TCP Endpoint repository, and build the ROS2 package:
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

### 2. Import Robot Model (URDF)
Similar to the previous step, import the URDF package into the Unity project. In the `Package Manager`, click the `+` button and select `Install package from git URL`, enter the following URL:
```
https://github.com/Unity-Technologies/URDF-Importer.git?path=/com.unity.robotics.urdf-importer#v0.5.2
```
Use the [turtlebot3](https://emanual.robotis.com/docs/en/platform/turtlebot3/features/#specifications) model as an example. As we have installed the `turtlebot3_description` package in the ROS2 workspace (use the burger model), we need to copy the URDF files to the Unity project. 
```bash
mkdir -p ws_unity/Assets/URDF
cp -r /opt/ros/jazzy/share/turtlebot3_description/urdf ws_unity/Assets/URDF
export TURTLEBOT3_MODEL=burger
```
The robot model is in xacro format
```bash
ros2 run xacro xacro $(ros2 pkg prefix turtlebot3_description)/share/turtlebot3_description/urdf/turtlebot3_burger.urdf.xacro > ws_unity/Assets/URDF/robot.urdf
```
Also copy the mesh files to the Unity project:
```bash
mkdir -p ~/socnav/ws_unity/Assets/URDF/meshes
cp -r $(ros2 pkg prefix turtlebot3_description)/share/turtlebot3_description/meshes/* ws_unity/Assets/URDF/meshes/
```
Since Unity cannot recognize the `package://` format, for instance
```xml
<mesh filename="package://turtlebot3_description/meshes/wheels/left_tire.stl" scale="0.001 0.001 0.001"/>
```
we need to change it to the relative path in the Unity project, for instance
```xml
<mesh filename="meshes/wheels/left_tire.stl" scale="0.001 0.001 0.001"/>
```
Go to the Unity Editor, right-click the `robot.urdf` file and select `Import Robot From the Selected URDF`. Use the default settings and click `Import URDF`. 

To prevent the robot from falling down, add a 3D Plane GameObject to the scene. The robot should be able to stand on the plane. To make the robot move with proper friction, use a low friction material for the caster wheel (base_footprint > base_link > caster_back_link > Collisions > unamed > Box, and change the material for the Box Collider).

### 3. Publish / Subscribe to ROS2 Topics
To communicate with ROS2, we need to create publishers and subscribers in Unity. 
```bash
mkdir -p ws_unity/Assets/Scripts/ROS
```
Create four new C# scripts named `CmdVelSubscriber.cs`, `OdometryPublisher.cs`, `TfPublisher.cs`, and `LaserScanPublisher.cs` in the `ws_unity/Assets/Scripts/ROS` directory.
These scripts are given in the corresponding place. Drag and drop the `CmdVelSubscriber` and `OdometryPublisher` scripts to `base_link`, `TfPublisher` scripts to the `base_footprint` GameObject, and `LaserScanPublisher` scripts to the `base_scan` GameObject.

To check if the communication is working, first run the ROS2 package in the terminal:
```bash
ros2 run ros_tcp_endpoint default_server_endpoint --ros-args -p ROS_IP:=127.0.0.1
```
Then in another terminal, source and check the topics:
```bash
ros2 topic list
```
You should see the following topics:
```
/cmd_vel
/odom
/parameter_events
/rosout
/scan
/tf
```

### 4. Nav2 with Static Map
Create a new ROS2 package for Nav2 launch files:
```bash
mkdir -p ws_ros2/src/socnav_bringup/launch
mkdir -p ws_ros2/src/socnav_bringup/config
touch ws_ros2/src/socnav_bringup/launch/nav2.launch.py
touch ws_ros2/src/socnav_bringup/config/nav2_params.yaml
touch ws_ros2/src/socnav_bringup/package.xml
touch ws_ros2/src/socnav_bringup/CMakeLists.txt
```
These files are given. Build the ROS2 package:
```bash
cd ws_ros2
colcon build --packages-select socnav_bringup
```
Start a static map server in the terminal:
```bash
ros2 run tf2_ros static_transform_publisher   --x 0 --y 0 --z 0   --qx 0 --qy 0 --qz 0 --qw 1   --frame-id map   --child-frame-id odom
```
Run the Nav2 launch file:
```bash
source install/setup.bash
ros2 launch socnav_bringup nav2.launch.py
```
Control it via keyboard teleop:
```bash
ros2 run teleop_twist_keyboard teleop_twist_keyboard
```
or remap:
```bash
ros2 run teleop_twist_keyboard teleop_twist_keyboard --ros-args -r /cmd_vel:=/namespace/cmd_vel
```

### 5. Nav2 with SLAM


### 6. Add Pedestrian
We will use AI Nav agent in Unity and add a publisher to publish the pedestrian's position to ROS2. Select the Plane GameObject (rename to Ground), click `Add Component`, and add `Nav Mesh Surface`. Click `Bake` to bake the navigation mesh. Create a new Capsule GameObject (rename to Ped), add `Nav Mesh Agent` component to it. Create a new C# script named `PedestrianAgent.cs` in the `ws_unity/Assets/Scripts/ROS` directory, and attach it to the Ped GameObject. Create a new empty GameObject (rename to ROSManager), and add `PedestrianPublisher.cs` script to it. Now the agent should be able to move around randomly, and the ROSManager should publish the pedestrian's position to ROS2. 
```bash
ros2 topic echo /pedestrians/poses --once
```