## [1.0.0-preview.11] - 2019-10-29

## [1.0.0-preview.10] - 2019-10-29

## [1.0.0-preview.9] - 2019-10-29

## [1.0.0-preview.8] - 2019-10-29


## [1.0.0-preview.7] - 2019-10-28

## [1.0.0-preview.6] - 2019-10-28

## [1.0.0-preview.5] - 2019-10-23

Release Candidate 1

## [1.0.0-preview.4] - 2019-10-17


## [1.0.0-preview.3] - 2019-10-09

Weekly Build

## [1.0.0-preview.2] - 2019-09-26

Weekly Build

## [1.0.0-preview.1] - 2019-09-26

Weekly Build

## [0.3.0-preview.8] - 2019-09-19
Features
Project discovery server
A new cloud-hosted server that allows for more reliable project discovery and enables user & project management. 
In 0.2, several people experienced discoverability issues, meaning that some viewers would not find projects on their local networks. Despite us releasing a hotfix, we thought a better solution was in order.
This new server will help make this process more stable and  allow for users to discover which projects are available to them. 
By default, all projects are shared only with the current user (yourself), but you can invite people to projects by managing projects here. After selecting a project, go to Settings->Users in the left-hand panel to manage permissions.
	Important Note: This new feature means that you now need to sign in into the Viewer to access you project. To do this, the Viewer will now open a web browser page, leading you to a sign in page. 

Common UI
The User interface in the different plugin is now shared, offering a more consistent experience between different 3rd party applications, and allowing users to create new projects from the plugins, without needing the Hub. This also allows us to re-use parts of the plugins and accelerate future developments

Bug Fixes
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

Changelog TODO
