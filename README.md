# CBCT calculator and replan prepper
Developed by Nicholas Nelson, University of Utah Huntsman Cancer Institute
## Description of code
This code utilizes the Eclipse scripting API (ESAPI) to perform dose calculations and preparations for replanning on CBCT images. In its current form,
online image registrations between the CBCT and planning image (CT simulation) will be utilized for isocenter placement and structure set transfers. 
The code will copy over the structures from the planning image to the CBCT using the rigid online registration slice-by-slice. If certain structures
do not fall on the CBCT domain, they will not be copied.
### CBCT naming convention
The user will be presented with a list of CBCT images and dates corresponding the creation of the image. In general, the CBCT's are labeled as kVCBCT_[FractionNumber][a/b/c]01, where
a, b, and c are incremented if the CBCT images are acquired in the same session.
## Current limitations
This script was developed using Eclipse 16.1, and therefore, the use of bolus is not currently supported. The code will simply warn the user that a bolus was
found and not copy it over. The use of bolus within ESAPI is supported in Eclipse 18.
## Screenshots
![Image1 of UI](https://github.com/user-attachments/assets/dd8a3efc-ff69-44d1-bf0a-fe95db3a538c)
![Image2 of UI](https://github.com/user-attachments/assets/b7e3d81e-1f1e-4138-aeb5-2d34ccab44ad)
