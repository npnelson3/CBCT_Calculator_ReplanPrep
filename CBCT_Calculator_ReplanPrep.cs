using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using VMS.TPS.Common.Model.API; // Version 18.1
using VMS.TPS.Common.Model.Types; // Version 18.1
using Image = VMS.TPS.Common.Model.API.Image;
using System.Windows.Media.Media3D; // this is my edit

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]

// Script needs write access
[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class StructureItem
    {
        public string Id { get; set; }
        public bool IsSelected { get; set; }
    }

    public class Script
    {
        public Script()
        {
        }
        // variable initialization
        private ComboBox _CBCTsComboBox;
        public System.Windows.Media.Brush Foreground { get; set; }
        private ComboBox _duplicatesComboBox;
        private TextBlock _TitleBlock;
        private TextBlock _AuthorBlock;
        private TextBlock _patientNameBlock;
        private TextBlock _patientInfoBlock;
        private TextBlock _CBCTnotesBlock;
        private TextBlock _notesBlock;
        private Button _calculateCBCTDoseButton;
        private Button _replanCBCTButton;
        private string _selectedCBCTofInterestString;
        private Common.Model.API.Image _cbctForCalculation;
        private Common.Model.API.Image _simImage;
        private Patient _patient;
        private Course _course;
        private PlanSetup _plan;
        private PlanSetup _planSetup;
        private StructureSet _structureSet;
        private IEnumerable<Beam> _OriginalBeams;
        private Registration _cbctRegistration;
        private IOrderedEnumerable<VMS.TPS.Common.Model.API.Image> _SortedImageList;
        private List<Common.Model.API.Image> _ImageList;
        private IEnumerable<Study> _studies;
        private ExternalPlanSetup _planExternalSetup;
        private List<KeyValuePair<string, MetersetValue>> presetValues;
        private Structure CBCTstructure;
        private ListBox _targetStructuresListBox;
        private List<StructureItem> _targetStructureItems;
        //public static SearchBodyParameters highDensityParameters;
        //private Structure HighDensity;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context, System.Windows.Window window/*, ScriptEnvironment environment*/)
        {
            // Validate patient in current context
            ValidatePatient(context);
            // Validate course in current context
            ValidateCourse(context);
            try
            {
                _patient = GetPatient(context);
                _course = GetCourse(context);
                _studies = GetStudy(context);
                _plan = GetPlan(context);
                _planSetup = GetPlanSetup(context);
                _planExternalSetup = context.ExternalPlanSetup;
                _OriginalBeams = GetBeams(context);
                _simImage = GetSimImage(context);
                _structureSet = GetStructureSet(context);

                //MessageBox.Show("Loaded patient, course, studies, and plan.");

                // set up a list of CBCT information (Id and date)
                _patient.BeginModifications(); // begin modifying patient data
                _ImageList = new List<VMS.TPS.Common.Model.API.Image>(); // generate a list of images
                foreach (var study in _studies)
                {   
                    foreach (var series in study.Series)
                    {
                        foreach (var image in series.Images)
                        {
                            if (image.Id.Contains("kV"))
                            {
                                _ImageList.Add(image);
                            }
                        }
                    }
                }
                // sort them based on creation datetime
                _SortedImageList = _ImageList.OrderBy(x => x.CreationDateTime);

                // present them to the user with the creation date in parenthesis
                var _SortedImageListWithDates = new List<string>();
                foreach (var image in _SortedImageList)
                {
                    _SortedImageListWithDates.Add(string.Format("{0} ({1} slices, {2})", image.Id, image.ZSize, image.CreationDateTime.ToString()));
                }

                // create a drop down list of structures with anything with CTV GTV PTV selected
                _targetStructureItems = _structureSet.Structures
                    .Where(s => !s.IsEmpty && s.DicomType != "EXTERNAL") // intentionally not showing the body on the list to avoid confusion
                    .OrderBy(s => s.Id)
                    .Select(s => new StructureItem
                    {
                        Id = s.Id,
                        IsSelected =
                            s.Id.StartsWith("PTV", StringComparison.OrdinalIgnoreCase) ||
                            s.Id.StartsWith("CTV", StringComparison.OrdinalIgnoreCase) ||
                            s.Id.StartsWith("GTV", StringComparison.OrdinalIgnoreCase)
                    })
                    .ToList();

                // if a patient has a replan within the same course, the replan CBCT's will start as kVCBCT_01a01 just like the original plan
                // for example if a patient had a replan for the last 5 fractions, there would be 5 duplicates
                var duplicatesList = _SortedImageList.GroupBy(x => x.Id).Where(g => g.Count() > 1).Select(y => y.Key).ToList();

                MessageBox.Show(string.Format("You are working with...\n\n" +
                    "Patient: {0}\n" +
                    "Course: {1}.\n" +
                    "Plan ID: {2}\n\n" +
                    "Following this, I will bring you to a new window where the CBCTs will be listed.", _patient.Name, _course.Id, _plan.Id));

                #region Starting to generate the UI
                // combo box for SortedImageList
                _CBCTsComboBox = new ComboBox
                {
                    //ItemsSource = SortedImageList,
                    ItemsSource = _SortedImageListWithDates,
                    //ItemsSource = _cbctIdsStringBuilder.ToString(),
                    // set the selected item as the plan target volume id OR the frist target in the list
                    //SelectedItem = string.IsNullOrEmpty(_plan.TargetVolumeID) ? _ptvIds.First() : _plan.TargetVolumeID,
                    Width = 300
                };

                // combo box for duplicates
                _duplicatesComboBox = new ComboBox
                {
                    // set the items source as structures that start with ptv
                    ItemsSource = duplicatesList,
                    //ItemsSource = _cbctIdsStringBuilder.ToString(),
                    // set the selected item as the plan target volume id OR the frist target in the list
                    //SelectedItem = string.IsNullOrEmpty(_plan.TargetVolumeID) ? _ptvIds.First() : _plan.TargetVolumeID,
                    Width = 300
                };

                // List of structures with check box
                _targetStructuresListBox = new ListBox
                {
                    Width = 300,
                    Height = 150,
                    ItemsSource = _targetStructureItems
                };
                FrameworkElementFactory factory = new FrameworkElementFactory(typeof(CheckBox));

                factory.SetBinding(CheckBox.ContentProperty,
                    new System.Windows.Data.Binding("Id"));

                factory.SetBinding(CheckBox.IsCheckedProperty,
                    new System.Windows.Data.Binding("IsSelected"));

                _targetStructuresListBox.ItemTemplate = new DataTemplate
                {
                    VisualTree = factory
                };

                // main container
                StackPanel spMain = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 10, 0, 0),
                    Width = 600
                };
                // title
                _TitleBlock = new TextBlock
                {
                    Text = "Automated CBCT calculator and replan prep",
                    FontSize = 32,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 10, 0, 0),
                    Foreground = System.Windows.Media.Brushes.MediumBlue
                };

                // author info
                _AuthorBlock = new TextBlock
                {
                    Text = "Developed by Nicholas Nelson, University of Utah Huntsman Cancer Institute (nicholas.nelson@hci.utah.edu)",
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };

                // Patient info
                _patientNameBlock = new TextBlock
                {
                    Text = "PATIENT INFO",
                    FontSize = 24,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                _patientInfoBlock = new TextBlock
                {
                    Text = string.Format("Name: {0}\n" +
                    "Course: {1}\n" +
                    "Plan ID: {2}\n", _patient.Name, _course.Id, _plan.Id),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 0)
                };

                _CBCTnotesBlock = new TextBlock
                {
                    Text = "CBCT naming convention details...",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    FontStyle = FontStyles.Italic,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 20, 0, 0)
                };

                _notesBlock = new TextBlock
                {
                    Text = string.Format("Each CBCT volume image has the following naming convention: kVCBCT_[FractionNumber][a/b/c/d]01.\n\n" +
                    "For example, kVCBCT_08a01 corresponds to the 8th fraction's CBCT. If two CBCT's are taken back-to-back, the letter following the fraction number will be incremented. " +
                    "For example, kVCBCT_08b01 would correspond to the 2nd CBCT image acquired, whose registration was most likely used for treatment."),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 5, 5, 0),
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Bottom
                };

                // CBCT Ids container and label
                StackPanel spCBCTs = new StackPanel()
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                spCBCTs.Children.Add(new TextBlock
                {
                    Text = "Choose CBCT",
                    FontWeight = FontWeights.Bold,
                    Width = 125,
                    Margin = new Thickness(0, 0, 10, 0)
                });
                spCBCTs.Children.Add(_CBCTsComboBox);

                // Duplicate Ids container and label
                StackPanel spDuplicates = new StackPanel()
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                spDuplicates.Children.Add(new TextBlock
                {
                    Text = "Duplicate list",
                    FontWeight = FontWeights.Bold,
                    Width = 125,
                    Margin = new Thickness(0, 0, 10, 0)
                });
                spDuplicates.Children.Add(_duplicatesComboBox);

                StackPanel spTargets = new StackPanel()
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                spTargets.Children.Add(new TextBlock
                {
                    Text = "Target Structures",
                    FontWeight = FontWeights.Bold,
                    Width = 125,
                    Margin = new Thickness(0, 0, 10, 0)
                });

                spTargets.Children.Add(_targetStructuresListBox);

                // button to calculate CBCT dose
                _calculateCBCTDoseButton = new Button
                {

                    // button content - what it says
                    Content = "Calculate dose on selected CBCT",

                    // a little padding
                    Padding = new Thickness(10),
                    Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Width = 260,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                // button to calculate CBCT dose
                _replanCBCTButton = new Button
                {

                    // button content - what it says
                    Content = "Prep for replanning using selected CBCT",

                    // a little padding
                    Padding = new Thickness(10),
                    Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Width = 260,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                #endregion End of ComboBox/StackPanel setup

                #region Calculation/replanning button clicking region
                _calculateCBCTDoseButton.Click += CalculateCBCTDoseButton_Click;
                _replanCBCTButton.Click += PrepCBCTReplan_Click;

                #endregion End of Calculation region

                #region Final UI presentation
                // add to main stack panel
                spMain.Children.Add(_TitleBlock);
                spMain.Children.Add(_AuthorBlock);
                spMain.Children.Add(_patientNameBlock);
                spMain.Children.Add(_patientInfoBlock);
                spMain.Children.Add(spCBCTs);
                spMain.Children.Add(spDuplicates);
                spMain.Children.Add(spTargets);
                //spMain.Children.Add(_calculateCBCTDoseButton); temporarily remove this button
                spMain.Children.Add(_replanCBCTButton);
                spMain.Children.Add(_CBCTnotesBlock);
                spMain.Children.Add(_notesBlock);
                spMain.VerticalAlignment = VerticalAlignment.Stretch;



                // window settings
                window.Title = "CBCT calculator and replan prepper";
                window.FontFamily = new System.Windows.Media.FontFamily("Calibri");
                window.FontSize = 14;
                window.Width = spMain.Width + 50;
                //window.Height = spMain.Height + 20;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Content = spMain;
                #endregion End of final UI presentation

            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Sorry, something went wrong.\n\n{0}\n\n{1}", ex.Message, ex.StackTrace));
                throw;
            }
        }

        //HELPER FUNCTIONS

        /// <summary>
        /// Gets the patient in the current context
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private static Patient GetPatient(ScriptContext context)
        {
            return context.Patient;
        }
        /// <summary>
        /// Gets the image of the current plan (which should be the CT sim)
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private static Common.Model.API.Image GetSimImage(ScriptContext context)
        {
            return context.Image;
        }

        private static IEnumerable<Beam> GetBeams(ScriptContext context)
        {
            return context.PlanSetup.Beams;
        }

        /// <summary>
        /// Returns the plan setup in the current context
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private static PlanSetup GetPlanSetup(ScriptContext context)
        {
            return context.PlanSetup;
        }

        private StructureSet GetStructureSet(ScriptContext context)
        {
            return context.StructureSet;
        }

        /// <summary>
        /// Gets plan
        /// </summary>
        /// <param name="context"></param>
        private PlanSetup GetPlan(ScriptContext context)
        {
            return context.PlanSetup;
        }

        /// <summary>
        /// Gets study
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private IEnumerable<Study> GetStudy(ScriptContext context)
        {
            return context.Patient.Studies;
        }

        /// <summary>
        /// Gets course
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private static Course GetCourse(ScriptContext context)
        {
            return context.Course;
        }

        /// <summary>
        /// Validates that the current context is a Course
        /// <para></para>Will alert the user and end the script
        /// </summary>
        /// <param name="context"></param>
        private void ValidateCourse(ScriptContext context)
        {
            if (context.Course == null)
            {
                MessageBox.Show("Please open a course");
                return;
            }
        }

        /// <summary>
        /// Validates that the current context is a Patient
        /// <para></para>Will alert the user and end the script
        /// </summary>
        /// <param name="context"></param>
        private void ValidatePatient(ScriptContext context)
        {
            if (context.Patient == null)
            {
                MessageBox.Show("Please open a patient");
                return;
            }
        }

        /// <summary>
        /// Executes the replan prep routine, which calls PrepForReplan().
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PrepCBCTReplan_Click(object sender, RoutedEventArgs e)
        {
            ValidateCBCTSelection(_CBCTsComboBox.SelectedItem, "Please select a CBCT to replan with!");
            _selectedCBCTofInterestString = _CBCTsComboBox.SelectedItem.ToString();
            // extract date from _selectedCBCTofInterestString

            string trimmedString = _selectedCBCTofInterestString.Substring(0, 12); // All have 12 characters (kVCBCT_01a01 = 12 characters)

            // Now lets try to reference this string to the image from _ImageList
            _cbctForCalculation = _ImageList.FirstOrDefault(d => d.Id.Equals(trimmedString));

            // set the selected CBCT image series to Vault3 device
            if (string.IsNullOrEmpty(_cbctForCalculation.Series.ImagingDeviceId))
            {
                _cbctForCalculation.Series.SetImagingDevice("VAULT3 CT");
            }

            // registration work
            //_cbctRegistration = _patient.Registrations.FirstOrDefault(r => r.RegisteredFOR == _simImage.FOR && r.SourceFOR == _cbctForCalculation.FOR);
            _cbctRegistration = _patient.Registrations.OrderBy(x => x.CreationDateTime).FirstOrDefault(r => r.RegisteredFOR == _simImage.FOR && r.SourceFOR == _cbctForCalculation.FOR); // this method will pick the latest registration

            MessageBox.Show(string.Format("I found the following corresponding registrion information\n\n\n" +
                "Source image Id:\t{0} ({1})\n\n" +
                "Registered image Id:\t{2} ({3})\n\n" +
                "Registation Id:\t{4} ({5})\n\n", _simImage.Id, _simImage.CreationDateTime, _cbctForCalculation.Id, _cbctForCalculation.CreationDateTime, _cbctRegistration.Id, _cbctRegistration.CreationDateTime));

            PrepForReplan(_patient, _course, _plan, trimmedString, _cbctRegistration, _cbctForCalculation, _simImage, _planExternalSetup);
        }

        public bool PrepForReplan(Patient _patient, Course _course, PlanSetup _plan, string trimmedString,
            Registration _cbctRegistration, Image _cbctForCalculation, Image _simImage, ExternalPlanSetup _planExternalSetup)
        {
            // search for a corresponding structure set for the CBCT image
            StructureSet CBCT_structureSet = _patient.StructureSets.FirstOrDefault(x => x.Id == trimmedString);
            if (CBCT_structureSet == null)
            {
                //MessageBox.Show(string.Format("Could not find a matching CBCT structure set named {0}...", trimmedString));
                return true;
            }

            StructureSet newCBCT_structureSet; // CBCT structure set that gets generated from GenerateNewImageAndCBCTStructureSet
            string fxNo;
            Image _newCBCT_image;
            // generate new CBCT image and contour high density (if needed)
            GenerateNewImageAndCBCTStructureSetAndContourBodyAndHighDensity(trimmedString, _cbctForCalculation, out newCBCT_structureSet, out fxNo, out _newCBCT_image);

            // get simulation image structure set
            StructureSet sim_structureSet = _patient.StructureSets.First(x => x.Id == _plan.StructureSet.Id);

            // get the isocenter shifts
            VVector IsoShift = GetIsocenterShifts(_cbctRegistration, _plan.Beams.First().IsocenterPosition);

            var selectedTargets = _targetStructureItems
                .Where(x => x.IsSelected).Select(x => x.Id).ToList();

            CopyStructuresToCBCTImage(_simImage, newCBCT_structureSet, _newCBCT_image, sim_structureSet, IsoShift, selectedTargets);

            // create new course for CBCT plan
            Course sourceCourse = _course;
            string newCourseId = sourceCourse.Id + "_OfflineEval";
            if (newCourseId.Length > 16)
            {
                newCourseId = newCourseId.Substring(0, 16);
            }
            // Check if it already exists
            Course offlineCourse = _patient.Courses
                .FirstOrDefault(c => c.Id == newCourseId);
            // If not, lets make it
            if (offlineCourse == null)
            {
                offlineCourse = _patient.AddCourse();
                offlineCourse.Id = newCourseId;
            }
            // add new plan
            var CBCT_plan = offlineCourse.AddExternalPlanSetupAsVerificationPlan(newCBCT_structureSet, _planExternalSetup);
            CBCT_plan.Id = string.Format("kVCBCT_fx{0}", fxNo);
            //MessageBox.Show("Added ExternalPlanSetup for CBCT plan!");

            //get beam info from other plan
            var getCollimatorAndGantryAngleFromBeam = (_plan as ExternalPlanSetup).Beams.Count() > 1; // not sure what this is doing

            // populate presetValues variable
            CopyBeamParameters(_patient, _plan, _cbctRegistration, _cbctForCalculation, _simImage, CBCT_plan);

            // copy optimization objectives for selected contours
            //CopyOptimizationObjectives(_plan, selectedTargets); //

            // calculate using presetValues
            CBCT_plan.SetPrescription((int)_planExternalSetup.NumberOfFractions, _planExternalSetup.DosePerFraction, _planExternalSetup.TreatmentPercentage);
            Structure TargetOnCBCT = CBCT_structureSet.Structures.FirstOrDefault(x => x.Id == _plan.TargetVolumeID);
            if (TargetOnCBCT != null)
            {
                StringBuilder myString = new StringBuilder(string.Format("Cannot set target structure to {0}!", TargetOnCBCT.Id));
                CBCT_plan.SetTargetStructureIfNoDose(TargetOnCBCT, myString);
            }

            MessageBox.Show(string.Format("Set the following prescription\n\n" +
                "Dose/fx: {0}\n" +
                "Number of fx: {1}\n" +
                "Total dose: {2}\n\n" +
                "At this point, please adjust relevant contours to reflect anatomical change and fractionation to reflect patient timeline.\n\n" +
                "Click 'ok' to exit and proceed with planning...", _plan.DosePerFraction, (int)_plan.NumberOfFractions, _plan.DosePerFraction * (int)_plan.NumberOfFractions));
            return false;
        }
        

        /// <summary>
        /// Executes the calculate dose button routine, which calls Calculate().
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CalculateCBCTDoseButton_Click(object sender, RoutedEventArgs e)
        {
            ValidateCBCTSelection(_CBCTsComboBox.SelectedItem, "Please select a CBCT to calculate on!");
            _selectedCBCTofInterestString = _CBCTsComboBox.SelectedItem.ToString();
            // extract date from _selectedCBCTofInterestString

            string trimmedString = _selectedCBCTofInterestString.Substring(0, 12); // All have 12 characters (kVCBCT_01a01 = 12 characters)

            // Now lets try to reference this string to the image from _ImageList
            _cbctForCalculation = _ImageList.FirstOrDefault(d => d.Id.Equals(trimmedString));

            // set the selected CBCT image series to Vault3 device
            if (string.IsNullOrEmpty(_cbctForCalculation.Series.ImagingDeviceId))
            {
                _cbctForCalculation.Series.SetImagingDevice("VAULT3 CT");
            }

            // registration work
            //_cbctRegistration = _patient.Registrations.FirstOrDefault(r => r.RegisteredFOR == _simImage.FOR && r.SourceFOR == _cbctForCalculation.FOR);
            _cbctRegistration = _patient.Registrations.OrderBy(x => x.CreationDateTime).FirstOrDefault(r => r.RegisteredFOR == _simImage.FOR && r.SourceFOR == _cbctForCalculation.FOR); // this method will pick the latest registration

            MessageBox.Show(string.Format("I found the following corresponding registrion information\n\n\n" +
                "Source image Id:\t{0} ({1})\n\n" +
                "Registered image Id:\t{2} ({3})\n\n" +
                "Registation Id:\t{4} ({5})\n\n", _simImage.Id, _simImage.CreationDateTime, _cbctForCalculation.Id, _cbctForCalculation.CreationDateTime, _cbctRegistration.Id, _cbctRegistration.CreationDateTime));

            Calculate(_patient, _course, _plan, trimmedString, _cbctRegistration, _cbctForCalculation, _simImage, _planExternalSetup);
        }

        /// <summary>
        /// Function in which many things are performed. First, a new CBCT image and SS will be generated to enforce the default material table. Second, the structures that land on the CBCT
        /// image will be copied over, slice by slice. Lastly, it will copy beam parameters over to a new verification plan and perform the dose calculation.
        /// </summary>
        /// <param name="_patient"></param>
        /// <param name="_course"></param>
        /// <param name="_plan"></param>
        /// <param name="trimmedString"></param>
        /// <param name="_cbctRegistration"></param>
        /// <param name="_cbctForCalculation"></param>
        /// <param name="_simImage"></param>
        /// <param name="_planExternalSetup"></param>
        /// <returns></returns>
        public bool Calculate(Patient _patient, Course _course, PlanSetup _plan, string trimmedString, Registration _cbctRegistration,
            Image _cbctForCalculation, Image _simImage, ExternalPlanSetup _planExternalSetup)
        {
            // search for a corresponding structure set for the CBCT image
            StructureSet CBCT_structureSet = _patient.StructureSets.FirstOrDefault(x => x.Id == trimmedString);
            if (CBCT_structureSet == null)
            {
                //MessageBox.Show(string.Format("Could not find a matching CBCT structure set named {0}...", trimmedString));
                return true;
            }

            StructureSet newCBCT_structureSet; // CBCT structure set that gets generated from GenerateNewImageAndCBCTStructureSet
            string fxNo;
            Image _newCBCT_image;
            // segment high density volume
            //highDensityParameters.LoadDefaults();
            //highDensityParameters.LowerHUThreshold = 3069;
            GenerateNewImageAndCBCTStructureSetAndContourBodyAndHighDensity(trimmedString, _cbctForCalculation, out newCBCT_structureSet, out fxNo, out _newCBCT_image);


            // get simulation image structure set
            StructureSet sim_structureSet = _patient.StructureSets.First(x => x.Id == _plan.StructureSet.Id);

            // get the isocenter shifts
            VVector IsoShift = GetIsocenterShifts(_cbctRegistration, _plan.Beams.First().IsocenterPosition);

            // generate selected contours
            var selectedTargets = _targetStructureItems
            .Where(x => x.IsSelected).Select(x => x.Id).ToList();

            //copy them over
            CopyStructuresToCBCTImage(_simImage, newCBCT_structureSet, _newCBCT_image, sim_structureSet, IsoShift, selectedTargets);

            // add new plan
            var CBCT_plan = _course.AddExternalPlanSetupAsVerificationPlan(newCBCT_structureSet, _planExternalSetup);
            CBCT_plan.Id = string.Format("kVCBCT_fx{0}", fxNo);
            //MessageBox.Show("Added ExternalPlanSetup for CBCT plan!");

            //get beam info from other plan
            var getCollimatorAndGantryAngleFromBeam = (_plan as ExternalPlanSetup).Beams.Count() > 1; // not sure what this is doing

            // populate presetValues variable
            CopyBeamParameters(_patient, _plan, _cbctRegistration, _cbctForCalculation, _simImage, CBCT_plan);

            // calculate using presetValues
            CBCT_plan.SetPrescription((int)_planExternalSetup.NumberOfFractions, _planExternalSetup.DosePerFraction, _planExternalSetup.TreatmentPercentage);
            Structure TargetOnCBCT = CBCT_structureSet.Structures.FirstOrDefault(x => x.Id == _plan.TargetVolumeID);
            if (TargetOnCBCT != null)
            {
                StringBuilder myString = new StringBuilder(string.Format("Cannot set target structure to {0}!", TargetOnCBCT.Id));
                CBCT_plan.SetTargetStructureIfNoDose(TargetOnCBCT, myString);
            }

            //MessageBox.Show(string.Format("Carried over the following settings from the original plan\n\n" +
            // "Dose/fx: {0}\n" +
            // "Number of fx: {1}\n" +
            // "Total dose: {2}\n", _plan.DosePerFraction, (int)_plan.NumberOfFractions, _plan.DosePerFraction * (int)_plan.NumberOfFractions),
            //"Plan normalization value (%): " + _plan.PlanNormalizationValue.ToString() + "%\n\n\n" +
            //"Copied parameters for all treatment fields.\n" +
            //"Click 'ok' to proceed to dose calculation (may take a few minutes)");


            MessageBox.Show(string.Format("Carried over the following settings from the original plan\n\n" +
                "Dose/fx: {0}\n" +
                "Number of fx: {1}\n" +
                "Total dose: {2}\n" +
                "Plan normalization value: " + _plan.PlanNormalizationValue.ToString() + "%\n\n\n" +
                "Copied parameters for all treatment fields.\n" +
                "Click 'ok' to proceed to dose calculation (may take a few minutes)", _plan.DosePerFraction, (int)_plan.NumberOfFractions, _plan.DosePerFraction * (int)_plan.NumberOfFractions));

            //var res = CBCT_plan.CalculateDoseWithPresetValues(presetValues);
            var res = CBCT_plan.CalculateDose();

            if (!res.Success)
            {
                var message = string.Format("Dose calculation failed for CBCT plan. Output:\n{0}", res);

                throw new Exception(message);
            }


            // set plan normalization
            CBCT_plan.PlanNormalizationValue = _plan.PlanNormalizationValue;
            // or else, let them know it worked
            MessageBox.Show("Dose calculation complete!");
            return false;
        }

        private static void CopyBeamParameters(Patient _patient, PlanSetup _plan, Registration _cbctRegistration, Image _cbctForCalculation, Image _simImage, ExternalPlanSetup CBCT_plan)
        {
            foreach (Beam field in _plan.Beams)
            {
                if (field.IsSetupField) // setup field CBCT
                {
                    var ImagingParameters = new ImagingBeamSetupParameters(ImagingSetup.kVCBCT, 0, 0, 0, 0, 280, 280); // 28 cm x 28 cm
                    var MachineParametersForImaging = new ExternalBeamMachineParameters(field.TreatmentUnit.Id);
                    CBCT_plan.AddImagingSetup(MachineParametersForImaging, ImagingParameters, CBCT_plan.StructureSet.Structures.FirstOrDefault(x => x.Id == CBCT_plan.TargetVolumeID));

                }
                else // copy beam data to presetValues
                {
                    var presetValues = CopyBeam(field, CBCT_plan, field.IsocenterPosition, _cbctRegistration, _cbctForCalculation, _patient, _simImage);
                }
            }
        }

        private void CopyOptimizationObjectives(
            PlanSetup _plan,
            List<string> selectedTargets)
        {

        }

        private void CopyStructuresToCBCTImage(
            Image _simImage,
            StructureSet newCBCT_structureSet,
            Image _newCBCT_image,
            StructureSet sim_structureSet,
            VVector IsoShift,
            List<string> selectedStructureIds)
        {
            int z_offset_start = (int)Math.Round((_newCBCT_image.Origin.z - _simImage.Origin.z) / (_newCBCT_image.ZRes));
            double z_cbct_end = _newCBCT_image.Origin.z + _newCBCT_image.ZRes * (_newCBCT_image.ZSize - 1);
            double z_ct_end = _simImage.Origin.z + _simImage.ZRes * (_simImage.ZSize - 1);
            int z_offset_end = (int)Math.Round((z_ct_end - z_cbct_end) / _newCBCT_image.ZRes);
            int z_index_offset = z_offset_start - (int)Math.Round(IsoShift.z / _newCBCT_image.ZRes);

            bool couchwarn = false;

            // Always include EXTERNAL so dose calculation works
            var external = sim_structureSet.Structures.FirstOrDefault(x => x.DicomType == "EXTERNAL");
            if (external != null && !selectedStructureIds.Contains(external.Id))
            {
                selectedStructureIds.Add(external.Id);
            }

            foreach (var structure in sim_structureSet.Structures)
            {
                // Skip structures not selected
                if (!selectedStructureIds.Contains(structure.Id) &&
                    structure.DicomType != "SUPPORT")
                {
                    continue;
                }

                // set CTV/GTV/PTV targets to have zz in front of ID and call it newID
                string newId = GetCBCTStructureId(structure, selectedStructureIds);

                // check if it already exists
                bool existsInCBCTStructureAlready =
                    newCBCT_structureSet.Structures.Any(x => x.Id == newId);

                int creationCounter = 0;

                if (!existsInCBCTStructureAlready)
                {
                    if (structure.DicomType == "BOLUS")
                    {
                        MessageBox.Show("It looks like there is a bolus in this plan, I cannot copy those over in Eclipse 16.1, should be available in Eclipse 18.");
                    }
                    else if (structure.DicomType == "EXTERNAL")
                    {
                        // skip copying over previous external
                    }
                    else if (structure.DicomType == "SUPPORT")
                    {
                        if (!couchwarn)
                        {
                            MessageBox.Show("I have found a couch in the structure set, will not copy over but instead insert the Halcyon couch.");
                            couchwarn = true;

                            bool imageResized = false;
                            string errorCouch = null;

                            newCBCT_structureSet.AddCouchStructures(
                                "RDS_Couch_Top",
                                PatientOrientation.NoOrientation,
                                RailPosition.In,
                                RailPosition.Out,
                                null,
                                null,
                                null,
                                out IReadOnlyList<Structure> couchStructureList,
                                out imageResized,
                                out errorCouch);
                        }
                    }
                    else
                    {
                        for (var z = 0; z < _newCBCT_image.ZSize; z++)
                        {
                            var contourOnImagePlane =
                                structure.GetContoursOnImagePlane(z + z_index_offset);

                            if (contourOnImagePlane != null && contourOnImagePlane.Length > 0)
                            {
                                if (creationCounter == 0)
                                {
                                    if (string.IsNullOrEmpty(structure.DicomType))
                                    {
                                        string DicomType = "ORGAN"; // set it to an organ
                                        CBCTstructure =
                                            newCBCT_structureSet.AddStructure(DicomType, newId);
                                        creationCounter = 1;
                                    }
                                    else
                                    {
                                        CBCTstructure =
                                            newCBCT_structureSet.AddStructure(structure.DicomType, newId);
                                        creationCounter = 1;
                                    }
                                }

                                CBCTstructure.Color = structure.Color;

                                foreach (var contour in contourOnImagePlane)
                                {
                                    VVector[] newContour = new VVector[contour.Length];
                                    for (int k=0;k<contour.Length; k++)
                                    {
                                        var pt = contour[k];
                                        newContour[k] = new VVector(pt.x + IsoShift.x, pt.y + IsoShift.y, pt.z + IsoShift.z);
                                    }

                                    CBCTstructure.AddContourOnImagePlane(newContour, z);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a new image and structure set to enforce the default material table. The default material table is only applied when the SS is made within Eclipse, so simply
        /// copying it does not work. And since every SS has to have an associated image, the image is also duplicated.
        /// </summary>
        /// <param name="trimmedString"></param>
        /// <param name="_cbctForCalculation"></param>
        /// <param name="newCBCT_structureSet"></param>
        /// <param name="fxNo"></param>
        /// <param name="_newCBCT_image"></param>
        private static void GenerateNewImageAndCBCTStructureSetAndContourBodyAndHighDensity(string trimmedString, Image _cbctForCalculation, 
            out StructureSet newCBCT_structureSet, out string fxNo, out Image _newCBCT_image)
        {
            newCBCT_structureSet = _cbctForCalculation.CreateNewStructureSet();
            string newCBCT_Id = trimmedString.Substring(7, 2); // grabbing 2 values in string, starting from 8. kVCBCT_01a01 should yield 01, nobody has over 100 fx's.
            newCBCT_Id = newCBCT_Id.TrimStart('0');
            fxNo = newCBCT_Id;
            newCBCT_Id = string.Format("Fraction {0} SS", newCBCT_Id);
            newCBCT_structureSet.Id = newCBCT_Id;
            // get the image associated with new structure set
            _newCBCT_image = newCBCT_structureSet.Image;
            _newCBCT_image.Id = string.Format("CBCT_{0}", fxNo); // Call it CBCT_FractionNumber (e.g. CBCT_6)

            // this new image won't be associated with VAULT3, so lets do that
            if (string.IsNullOrEmpty(_newCBCT_image.Series.ImagingDeviceId))
            {
                _newCBCT_image.Series.SetImagingDevice("VAULT3 CT");
                //MessageBox.Show(string.Format("Set {0} imaging device to {1}!", _newCBCT_image.Id, _newCBCT_image.Series.ImagingDeviceId));
            }

            // contour the body on the new CBCT SS
            Structure body = newCBCT_structureSet.Structures.FirstOrDefault(s => s.DicomType == "EXTERNAL");
            if (body == null)
            {
                SearchBodyParameters defaultSearchParameters = newCBCT_structureSet.GetDefaultSearchBodyParameters();
                body = newCBCT_structureSet.CreateAndSearchBody(defaultSearchParameters);
                //MessageBox.Show(string.Format("Searching for body using default parameters, LowerHUThreshold = {0}.", defaultSearchParameters.LowerHUThreshold));
            }
        }
        /// <summary>
        /// Renames structures to add zz before targets (CTV/GTV/PTV)
        /// </summary>
        /// <param name="structure"></param>
        /// <param name="selectedStructureIds"></param>
        /// <returns></returns>
        private string GetCBCTStructureId(Structure structure, List<string> selectedStructureIds)
        {
            string id = structure.Id;

            bool isTarget =
                id.IndexOf("PTV", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("CTV", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("GTV", StringComparison.OrdinalIgnoreCase) >= 0;

            if (selectedStructureIds.Contains(id) &&
                isTarget &&
                !id.StartsWith("zz", StringComparison.OrdinalIgnoreCase))
            {
                return "zz" + id;
            }

            return id;
        }

        /// <summary>
        /// Relates the z-index from the CBCT to the z-index of the CTsim image and structure set.
        /// </summary>
        /// <param name="z"></param>
        /// <param name="zRes"></param>
        /// <param name="IsoShiftZ"></param>
        /// <returns></returns>
        public int GetCTPlaneFromCBCTIndex(int z, double zRes, double IsoShiftZ)
        {
            var wantedslicedisplacement = IsoShiftZ;
            var plane = (int)Math.Round((wantedslicedisplacement) / (zRes), 1);
            return z + plane;
        }

        /// <summary>
        /// Calculates isocenter shifts based on registration in the X, Y, and Z direction.
        /// </summary>
        /// <param name="cbctRegistration"></param>
        /// <param name="isocenterPosition"></param>
        /// <returns></returns>
        public VVector GetIsocenterShifts(Registration cbctRegistration, VVector isocenterPosition)
        {
            VVector cbctIsocenter = cbctRegistration.InverseTransformPoint(isocenterPosition);
            VVector IsoShift = cbctIsocenter - isocenterPosition;
            return IsoShift;
        }

        public static MeshGeometry3D SolveMeshReference(MeshGeometry3D meshGeometry3D)
        {
            var meshClone = meshGeometry3D.CloneCurrentValue();
            //meshClone.TriangleIndices = null;
            return meshClone;
        }

        /// <summary>
        /// Checks to see if the user selected a CBCT from the drop down menu and will terminate if none is selected.
        /// </summary>
        /// <param name="selectedItem"></param>
        /// <param name="alertmessage"></param>
        private void ValidateCBCTSelection(object selectedItem, string alertmessage)
        {
            if (selectedItem == null)
            {
                MessageBox.Show(alertmessage);
                return;
            }
        }

        /// <summary>
        /// Create a copy of an existing beam (beams are unique to plans).
        /// </summary>
        private static string CopyBeam(Beam originalBeam, ExternalPlanSetup plan, VVector isocenter, Registration cbctRegistration, VMS.TPS.Common.Model.API.Image cbctImage, Patient patient,
            VMS.TPS.Common.Model.API.Image simImage)
        {

            ExternalBeamMachineParameters MachineParameters =
                new ExternalBeamMachineParameters(originalBeam.TreatmentUnit.Id, "6X", // used to be originalBeam.EnergyModeDisplayName
                originalBeam.DoseRate, originalBeam.Technique.Id,
                "FFF"); // or string.Empty()

            //ExternalBeamMachineParameters MachineParameters =
            //    new ExternalBeamMachineParameters(originalBeam.TreatmentUnit.Id); // or string.Empty()

            //MachineParameters.MLCId = "SX2";
            //MessageBox.Show(string.Format("MLC Id is {0}", MachineParameters.MLCId));

            //MessageBox.Show("Made it out of the Machine Parameters setting line");

            // Create a new beam.
            var collimatorAngle = originalBeam.ControlPoints.First().CollimatorAngle;
            var gantryAngleStart = originalBeam.ControlPoints.First().GantryAngle;
            var gantryAngleStop = originalBeam.ControlPoints.Last().GantryAngle;
            var gantryDirection = originalBeam.GantryDirection;
            var couchAngle = originalBeam.ControlPoints.First().PatientSupportAngle;
            var metersetWeights = originalBeam.ControlPoints.Select(cp => cp.MetersetWeight);

            //MessageBox.Show(string.Format("I have entered the 'CopyBeam' function, here are parameters for:\n\n" +
            //    "Treatment Unit ID: {0}\n" +
            //    "EnergyModeDisplayName: {1}\n" +
            //    "Dose Rate: {2}\n" +
            //    "Technique ID: {3}\n" +
            //    "Beam Id: {4}\n" +
            //    "Collimator angle: {5}\n" +
            //    "Couch angle: {6}\n" +
            //    "MU: {7}\n" +
            //    "Gantry start angle: {8}\n" +
            //    "Gantry stop angle: {9}\n" +
            //    "Simulation isocenter (x, y, z):({10},{11},{12})", originalBeam.TreatmentUnit.Id, originalBeam.EnergyModeDisplayName,
            //    originalBeam.DoseRate, originalBeam.Technique.Id, originalBeam.Id, collimatorAngle, originalBeam.ControlPoints.First().PatientSupportAngle,
            //    originalBeam.Meterset.Value, gantryAngleStart, gantryAngleStop, isocenter.x, isocenter.y, isocenter.z));

            // at this point, isocenter values need to be changed to the basis of the CBCT.
            // For example isocenter on the CT sim scan is (0,-20.95, -4) [cm], and the CBCT isocenter (post-reg) is (-0.1, 0.14, -0.47)
            var cbctIsocenter = cbctRegistration.InverseTransformPoint(isocenter);

            //MessageBox.Show(string.Format("Below are the isocenters referenced on the two images frame of reference\n\n" +
            //    "{0} isocenter (x,y,z): ({1}, {2}, {3})\n\n" +
            //    "{4} isocenter (x,y,z): ({5}, {6}, {7})", simImage.Id, isocenter.x, isocenter.y, isocenter.z, cbctImage.Id, cbctIsocenter.x, cbctIsocenter.y, cbctIsocenter.z));

            #region VMAT BEAM METHOD
            //// VMAT BEAM METHOD
            var beam = plan.AddVMATBeamForFixedJaws(MachineParameters, metersetWeights, collimatorAngle, gantryAngleStart, gantryAngleStop, gantryDirection, couchAngle, cbctIsocenter);
            //beam.RemoveFlatteningSequence();

            //MessageBox.Show("Created a new beam!");

            // Copy control points from the original beam.
            var editableParams = beam.GetEditableParameters();
            for (var i = 0; i < editableParams.ControlPoints.Count(); i++)
            {
                editableParams.ControlPoints.ElementAt(i).LeafPositions = originalBeam.ControlPoints.ElementAt(i).LeafPositions;
            }
            editableParams.WeightFactor = originalBeam.WeightFactor; // apply weigthing from old beam
            //MessageBox.Show("Made it to just before 'beam.ApplyParameters(editableParams)'");
            beam.Id = originalBeam.Id;
            beam.ApplyParameters(editableParams);


            #endregion


            //MessageBox.Show(string.Format("Wrote parameters to beam {0}", beam.Id));

            return beam.Id;
        }



    }
}


