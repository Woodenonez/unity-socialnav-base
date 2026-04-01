from setuptools import setup, find_packages
from glob import glob
import os

package_name = 'tiago_omni_description'


def package_files(directory_list):
    paths_dict = {}

    for directory in directory_list:
        for path, _, filenames in os.walk(directory):
            for filename in filenames:
                file_path = os.path.join(path, filename)
                install_path = os.path.join('share', package_name, path)
                paths_dict.setdefault(install_path, []).append(file_path)

    return list(paths_dict.items())


data_files = [
    ('share/ament_index/resource_index/packages',
     ['resource/' + package_name]),
    ('share/' + package_name, ['package.xml']),
]

data_files += package_files([
    'config',
    'gazebo',
    'meshes',
    'robots',
    'ros2_control',
    'urdf',
    # 'launch', 
])

setup(
    name=package_name,
    version='0.0.1',
    packages=find_packages(exclude=['test']),
    data_files=data_files,
    install_requires=['setuptools'],
    zip_safe=True,
    maintainer='Your Name',
    maintainer_email='you@example.com',
    description='Robot description and Python ROS 2 nodes for my robot.',
    license='Apache-2.0',
    tests_require=['pytest'],
    entry_points={
        'console_scripts': [
            # 'state_pub = my_robot_pkg.state_pub:main',
            # 'controller = my_robot_pkg.controller:main',
        ],
    },
)