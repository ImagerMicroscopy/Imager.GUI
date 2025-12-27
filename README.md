# Imager GUI  

[<img src="docs/images/logo.png" align="right" width="100">]


*A cross-platform UI for Imager, built with Avalonia*


---

## Overview

This repository contains the Avalonia-based GUI for **Imager**, including UI components and communication utilities. It is designed to be cross-platform and developer-friendly.

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
   


