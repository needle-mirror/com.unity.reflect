## [1.2.0-preview.26] - 2020-06-17

## [1.2.0-preview.25] - 2020-06-16

## [1.2.0-preview.24] - 2020-06-16

## [1.2.0-preview.23] - 2020-06-15

## [1.2.0-preview.22] - 2020-06-11

## [1.2.0-preview.21] - 2020-06-09

## [1.2.0-preview.20] - 2020-06-06

# Features
Navisworks Plugin: Navisworks 2019, 2020, 2021
Note: We support geometry, materials and metadata. Navisworks supports a very wide range of data, and Reflect will not support everything in this first version.

Revit Plugin: Revit 2021 Support
Note: Revit 2018 is still supported, but will not be maintained, following Autodesk’s policy of supporting the past 3 versions

Sketchup Plugin: Sketchup 2020 Support

Editor Workflow: A new tool in Reflect allows you to convert between Built-in, URP & HDRP

# Bug Fixes
UI: Fixed an issue where the Reflect UI would sometimes be stuck behind Revit, making it look like Revit crashed.

Reflect: Fixed a crash in server when exporting very large models, due to running out of memory
Reflect: Fixed a bug where merged objects would lose some metadata
Revit crashed.
Reflect: Fixed an issue where Reflect would fail if a single mesh was bigger than 128MB. New limit for a single object is 2GB.
Reflect: Fixed a “missing Assembly Error” when building Reflect for OSX

Sync: Fixed an issue where moving linked Revit files while sync is active would create a duplicate of the linked file

Rhino: Fixed an issue where Rhino model orientation was sometimes wrong
Rhino: Rhino “Document” filter is now available
Rhino: Fixed an issue with Rhino unit conversions
Rhino: Fixed an issue where Rhino would send objects twice

Sketchup: Fixed an issues where “document” filter was not available for data coming from Sketchup
Sketchup: Sketchup “Document” filter is now available
Sketchup: Fixed an issues where “document” filter was not available for data coming from Sketchup

## [1.0.0-preview.15] - 2019-11-15

# Features
- Better out-of-the-box visual quality
- VR support, for HTC Vive!
- Revit 2020 support
- The ability to set a project server to Private/Public
# Bug Fixes:
- Fixed an issue where Revit would spin forever on certian machines with IT restrictions
- Fixed an issue where the Viewer was unable to login
- Better error emssaging and feedback overall

## [0.3.0-preview.8] - 2019-09-19
# Features
Project discovery server
A new cloud-hosted server that allows for more reliable project discovery and enables user & project management.
In 0.2, several people experienced discoverability issues, meaning that some viewers would not find projects on their local networks. Despite us releasing a hotfix, we thought a better solution was in order.
This new server will help make this process more stable and  allow for users to discover which projects are available to them.
By default, all projects are shared only with the current user (yourself), but you can invite people to projects by managing projects here. After selecting a project, go to Settings->Users in the left-hand panel to manage permissions.
	Important Note: This new feature means that you now need to sign in into the Viewer to access you project. To do this, the Viewer will now open a web browser page, leading you to a sign in page.

# Common UI
The User interface in the different plugin is now shared, offering a more consistent experience between different 3rd party applications, and allowing users to create new projects from the plugins, without needing the Hub. This also allows us to re-use parts of the plugins and accelerate future developments

# Bug Fixes
Fixed an issue where the Unity Hub would not discover Revit & Sketchup applications
Fixed an issue resulting in plugin installation hanging forever when a user doesn’t have permissions to write to a specific location
Fixed an issue where project list would randomly fail to populate

Limitations & Known Issues
Reflect projects from previous versions are incompatible
The Common UI does not have a progress bar for export progress yet. We rely on Revit’s built-in progress bar for now.
To install the Unity Reflect package in the editor, you will need to enable “Show Preview Packages” under the advanced options in the Package Manager.
The Viewer will automatically login to your account when launching. This allows it to know which projects you have access to, but it is a very un-polished experience as-is.
Reflect materials are only compatible with the Standard rendering pipeline. If you want to use LWRP or HDRP, you will need to replace all materials.
Changing a Clipping plane in Revit won’t update the model in Reflect. The model needs to be re-exported
Sketchup plugin is not working with the new Common UI. We are fixing it ASAP.
The Hub sometimes needs to be restarted twice after first launch
The Reflect Viewer does not automatically launch after exporting a project
The Viewer will ask for administrator rights the first time you run it

## [0.3.0-preview.5] - 2019-09-17
Viewer login
Reduce elevated permission prompts to 1
Displaying list of servers in common UI
Remove server check box and address fields in Editor
Change protocol to support multiple IP addresses when registering server
Never remove local projects from the project list in Viewer and Reflect Windows
Fix invalid package dependencies (SM)

## [0.3.0-preview.4] - 2019-09-15

CommonUI
Project Server

## [0.3.0-preview.3] - 2019-09-12

# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html)