# Imager GUI  



<img src="docs/images/logo.png" align="right" width="80">

 *A cross-platform UI for Imager, built with Avalonia*

This is the main repository containing the implementation of the graphical user interface for Imager. 


## Table of Contents

- [Overview](#overview)
- [Requirements](#requirements)
- [Installation & Running](#installation--running)
  - [Step 1: Clone the repository](#step-1-clone-the-repository)
  - [Step 2: Build the project](#step-2)
- [Documentation](#documentation)

## Overview

This repository contains the Avalonia-based GUI for **Imager**, including UI components and communication utilities. It is designed to be cross-platform and developer-friendly. 
Our UI implementation includes various features such as an interactive experiment designer, live image viewer, acquisition tabs, and many other useful utilities! 

<img src="docs/images/screenshot.png" align="center" width="700">


---

##  Requirements

Make sure the .NET runtime is installed. You can follow the installation instructions for windows at:

[Windows .NET installation](https://learn.microsoft.com/en-us/dotnet/core/install/windows)

And for linux at:

[Linux .NET installation](https://learn.microsoft.com/en-us/dotnet/core/install/linux)


> [!IMPORTANT]
> Make sure to use the .NET 10 version

---

##  Installation & Running

Follow these steps to install and run the project:

#### Step 1: **Clone the repository**

   Open terminal and clone the repository:
   ```bash
   git clone https://github.com/ImagerMicroscopy/Imager.GUI
   cd Imager.GUI
   ```

   
#### Step 2: 
   
   Build the release of the project:

   **For windows:**

   ```bash
    dotnet publish ImagerAvalonia.Dekstop -r win-x64 -p:PublishSingleFile=true --self-contained true  -o Release
   ``` 

   **For linux:**

   ```bash
    dotnet publish ImagerAvalonia.Dekstop -r linux-x64 -p:PublishSingleFile=true --self-contained true  -o Release
   ``` 

   This will create compiled binaries in the folder 'Release'

## Documentation

The full documentation about how to use the GUI can be found on the github pages
   


