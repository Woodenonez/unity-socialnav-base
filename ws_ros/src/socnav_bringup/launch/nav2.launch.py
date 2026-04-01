import os
from launch import LaunchDescription
from launch.actions import IncludeLaunchDescription, TimerAction
from launch.launch_description_sources import PythonLaunchDescriptionSource
from launch_ros.actions import Node
from ament_index_python.packages import get_package_share_directory


def generate_launch_description():

    socnav_dir  = get_package_share_directory('socnav_bringup')
    nav2_params = os.path.join(socnav_dir, 'config', 'nav2_params.yaml')
    slam_params = os.path.join(socnav_dir, 'config', 'slam_params.yaml')

    # ------------------------------------------------------------------ #
    #  SLAM toolbox — builds map from /scan, publishes map->odom TF       #
    # ------------------------------------------------------------------ #
    slam = IncludeLaunchDescription(
        PythonLaunchDescriptionSource(
            os.path.join(
                get_package_share_directory('slam_toolbox'),
                'launch', 'online_async_launch.py'
            )
        ),
        launch_arguments={
            'slam_params_file': slam_params,
            'use_sim_time': 'true',
        }.items(),
    )

    # ------------------------------------------------------------------ #
    #  Static TF publishers                                                #
    #  Fixed transforms from TurtleBot3 burger URDF.                      #
    #  Kept in ROS2 (not Unity) so they never expire.                     #
    # ------------------------------------------------------------------ #

    # base_footprint -> base_link  (10 mm ground clearance)
    tf_footprint_to_link = Node(
        package='tf2_ros',
        executable='static_transform_publisher',
        name='tf_base_footprint_to_base_link',
        arguments=['0', '0', '0.010', '0', '0', '0',
                   'base_footprint', 'base_link'],
    )

    # base_link -> base_scan  (LiDAR: -32 mm forward, 172 mm up)
    tf_link_to_scan = Node(
        package='tf2_ros',
        executable='static_transform_publisher',
        name='tf_base_link_to_base_scan',
        arguments=['-0.032', '0', '0.172', '0', '0', '0',
                   'base_link', 'base_scan'],
    )

    # map -> odom  (identity placeholder)
    # Prevents "map frame does not exist" errors during startup.
    # SLAM toolbox will override this once it has processed enough scans.
    tf_map_to_odom = Node(
        package='tf2_ros',
        executable='static_transform_publisher',
        name='tf_map_to_odom_init',
        arguments=['0', '0', '0', '0', '0', '0', 'map', 'odom'],
    )

    # ------------------------------------------------------------------ #
    #  Nav2 nodes                                                          #
    #  Launched individually — excludes docking_server and route_server   #
    # ------------------------------------------------------------------ #

    controller_server = Node(
        package='nav2_controller',
        executable='controller_server',
        name='controller_server',
        output='screen',
        parameters=[nav2_params],
        remappings=[('cmd_vel', 'cmd_vel_nav')],
    )

    smoother_server = Node(
        package='nav2_smoother',
        executable='smoother_server',
        name='smoother_server',
        output='screen',
        parameters=[nav2_params],
    )

    planner_server = Node(
        package='nav2_planner',
        executable='planner_server',
        name='planner_server',
        output='screen',
        parameters=[nav2_params],
    )

    behavior_server = Node(
        package='nav2_behaviors',
        executable='behavior_server',
        name='behavior_server',
        output='screen',
        parameters=[nav2_params],
        remappings=[('cmd_vel', 'cmd_vel_nav')],
    )

    bt_navigator = Node(
        package='nav2_bt_navigator',
        executable='bt_navigator',
        name='bt_navigator',
        output='screen',
        parameters=[nav2_params],
    )

    waypoint_follower = Node(
        package='nav2_waypoint_follower',
        executable='waypoint_follower',
        name='waypoint_follower',
        output='screen',
        parameters=[nav2_params],
    )

    # velocity_smoother sits between controller and robot:
    #   controller  -->  cmd_vel_nav  -->  velocity_smoother  -->  cmd_vel  -->  robot
    velocity_smoother = Node(
        package='nav2_velocity_smoother',
        executable='velocity_smoother',
        name='velocity_smoother',
        output='screen',
        parameters=[nav2_params],
        remappings=[
            ('cmd_vel',          'cmd_vel_nav'),
            ('cmd_vel_smoothed', 'cmd_vel'),
        ],
    )

    collision_monitor = Node(
        package='nav2_collision_monitor',
        executable='collision_monitor',
        name='collision_monitor',
        output='screen',
        parameters=[nav2_params],
    )

    # ------------------------------------------------------------------ #
    #  Lifecycle manager                                                   #
    #  Delayed 3 s so all nodes are registered before activation begins   #
    # ------------------------------------------------------------------ #
    lifecycle_manager = Node(
        package='nav2_lifecycle_manager',
        executable='lifecycle_manager',
        name='lifecycle_manager_navigation',
        output='screen',
        parameters=[{
            'autostart':    True,
            'bond_timeout': 4.0,
            'node_names': [
                'controller_server',
                'smoother_server',
                'planner_server',
                'behavior_server',
                'bt_navigator',
                'waypoint_follower',
                'velocity_smoother',
                'collision_monitor',
            ],
        }],
    )

    # ------------------------------------------------------------------ #
    #  RViz2                                                               #
    # ------------------------------------------------------------------ #
    rviz2 = Node(
        package='rviz2',
        executable='rviz2',
        name='rviz2',
        output='screen',
    )

    # ------------------------------------------------------------------ #
    #  Launch order                                                        #
    #  1. Static TFs   — frames must exist before anything else uses them #
    #  2. SLAM          — needs base_scan frame already published          #
    #  3. Nav2 nodes    — need map/odom frames                             #
    #  4. Lifecycle mgr — delayed 3 s, activates nodes after startup      #
    #  5. RViz2                                                            #
    # ------------------------------------------------------------------ #
    return LaunchDescription([
        # 1. Static TFs
        tf_footprint_to_link,
        tf_link_to_scan,
        tf_map_to_odom,

        # 2. SLAM
        slam,

        # 3. Nav2 nodes
        controller_server,
        smoother_server,
        planner_server,
        behavior_server,
        bt_navigator,
        waypoint_follower,
        velocity_smoother,
        collision_monitor,

        # 4. Lifecycle manager (delayed)
        TimerAction(period=3.0, actions=[lifecycle_manager]),

        # 5. RViz2
        rviz2,
    ])