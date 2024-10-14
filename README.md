# CBCT calculator and replan prepper
Developed by Nicholas Nelson, University of Utah Huntsman Cancer Institute
## Description of code
This code utilizes the Eclipse scripting API (ESAPI) to perform dose calculations and preparations for replanning on CBCT images. In its current form,
online image registrations between the CBCT and planning image (CT simulation) will be utilized for isocenter placement and structure set transfers. 
The code will copy over the structures from the planning image to the CBCT using the rigid online registration slice-by-slice. If certain structures
do not fall on the CBCT domain, they will not be copied.
### Field weighting and MUs
If the calculation is request, the field weighting and normalization value (and thus, the field MUs) will be carried over from the original plan.
If just replacing is request, the field weights are carried over but normalization is not set.
### High density overrides
The script will utilize the CreateAndSearchBody method to segment the high density structures. In its current form, the scipt will segment anything with an HU greater than 3069 and sets that
volume to have an HU of 3069.
### Couch insertion
The script will replace the couch from the original structure set with the Halcyon couch and insert it.
### CBCT naming convention
The user will be presented with a list of CBCT images and dates corresponding the creation of the image. In general, the CBCT's are labeled as kVCBCT_[FractionNumber][a/b/c]01, where
a, b, and c are incremented if the CBCT images are acquired in the same session.
## Current limitations
This script was developed using Eclipse 16.1, and therefore, the use of bolus is not currently supported. The code will simply warn the user that a bolus was
found and not copy it over. The use of bolus within ESAPI is supported in Eclipse 18. 

For many treatment sites, the longitudinal FOV may be limited to allow for full scatter conditions in the scan (e.g., primary beam entering regions where no CT data exits in sup/inf directions). 
To help combat this, the number of slices is listed in the drop down to help identify extended CBCT scans. Dosimetric integrity in general is maintained at the center of the FOV but degrades (~1-3% lower dose from CBCT) near the CBCT edges.
## Future work
-Add a second drop down bar that highlights CBCT's that have an extended FOV in the longitudinal/slice direction (in addition to duplicate list)
-Utilize the CalculateDose() method instead of CalculateDoseWithPresetValues() to allow for leverage of the distributed calculation network.

## Screenshots
![Image1 of UI](https://github.com/user-attachments/assets/dd8a3efc-ff69-44d1-bf0a-fe95db3a538c)
![Image2 of UI](https://github.com/user-attachments/assets/b7e3d81e-1f1e-4138-aeb5-2d34ccab44ad)
