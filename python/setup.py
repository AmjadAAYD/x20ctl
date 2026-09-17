from setuptools import setup, find_packages

with open("README.md", "r", encoding="utf-8") as fh:
    long_description = fh.read()

setup(
    name="x20ctl",
    version="2.0.0",
    author="AmjadAAYD",
    description="Python configuration suite and Windows standalone executable for the EasySMX X20 / KeyLinker gamepad",
    long_description=long_description,
    long_description_content_type="text/markdown",
    url="https://github.com/AmjadAAYD/x20ctl",
    py_modules=["app", "keylinker", "build_exe"],
    packages=find_packages(),
    classifiers=[
        "Programming Language :: Python :: 3",
        "Programming Language :: Python :: 3.10",
        "Programming Language :: Python :: 3.11",
        "Programming Language :: Python :: 3.12",
        "License :: OSI Approved :: MIT License",
        "Operating System :: Microsoft :: Windows",
        "Topic :: System :: Hardware :: Hardware Drivers",
    ],
    python_requires=">=3.8",
    install_requires=[
        "bleak>=0.21.0",
        "pygame>=2.5.0",
        "Pillow>=10.0.0",
    ],
    entry_points={
        "console_scripts": [
            "x20ctl=app:main",
        ],
    },
)
