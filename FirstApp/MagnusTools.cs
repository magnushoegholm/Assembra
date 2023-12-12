using Singleton;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace FirstApp
{

    // A set of different functions and tools used by Form1.cs and the toolbox itself
    internal static class MagnusTools
    {


        // Get the interferences between all components in the assembly
        private static string[][] detectInterferences(AssemblyDoc swAssembly, ModelDoc2 Document, bool print)
        {
            InterferenceDetectionMgr pIntMgr = default(InterferenceDetectionMgr);
            object[] vInts = null;
            long i;
            long j;
            IInterference interference = default(IInterference);
            object[] vComps = null;
            Component2 comp = default(Component2);
            double vol = 0;

            // Open the Interference Detection pane. It is visible in the window, if Solidworks is also visible
            swAssembly.ToolsCheckInterference();
            pIntMgr = swAssembly.InterferenceDetectionManager;


            // Specify the interference detection settings and options
            // Subassemblies are treated as components
            pIntMgr.TreatCoincidenceAsInterference = true;
            pIntMgr.TreatSubAssembliesAsComponents = true;
            pIntMgr.IncludeMultibodyPartInterferences = false;
            pIntMgr.MakeInterferingPartsTransparent = false;
            pIntMgr.CreateFastenersFolder = false;
            pIntMgr.IgnoreHiddenBodies = false;
            pIntMgr.ShowIgnoredInterferences = false;
            pIntMgr.UseTransform = false;


            //Run interference detection
            vInts = (object[])pIntMgr.GetInterferences();
            Debug.WriteLine("Running interference detection");
            Debug.WriteLine("Total number of interferences: " + pIntMgr.GetInterferenceCount());
            Debug.WriteLine("");


            // Declare output array of relevant size
            int nInts = pIntMgr.GetInterferenceCount();
            string[][] interferences = new string[nInts][];

            if (vInts == null)
            {
                return null;
            }

            // Print and save inteferences
            for (i = 0; i <= vInts.GetUpperBound(0); i++)
            {

                // Print information about interference
                conditionalPrint("   Interference " + (i + 1),print);
                interference = (IInterference)vInts[i];
                conditionalPrint("     Interference between " + interference.GetComponentCount() + " components:", print);
                vComps = (object[])interference.Components;

                // Make room in array
                interferences[i] = new string[2];

                // Print involved components and add pair to output
                for (j = 0; j <= vComps.GetUpperBound(0); j++)
                {
                    comp = (Component2)vComps[j];
                    conditionalPrint("      - " + comp.Name2, print);
                    //Debug.WriteLine("            " + comp.GetSelectByIDString());
                    interferences[i][j] = cleanUpID(comp.GetSelectByIDString());


                }
                vol = interference.Volume;

                // Possibility to print volume as well
                //Debug.WriteLine("     Interference volume is " + (vol * 1000000000) + " mm^3");
                //Debug.WriteLine("");
            }
            // Close interference manager
            pIntMgr.Done();

            return interferences;
        }


        // Compute the constraints based on the Drag Operator
        private static int[] computeConstraints2(AssemblyDoc swAssembly, ModelDoc2 Document, SldWorks swApp, string moveID, string staticID, double clearance, object[] allComponents)
        {

            //ModelDoc2 swModel = default(ModelDoc2);
            ModelDocExtension swModelDocExt = default(ModelDocExtension);
            //AssemblyDoc swAssy = default(AssemblyDoc);
            DragOperator swDragOp = default(DragOperator);
            SelectionMgr swSelMgr = default(SelectionMgr);
            Component2 swComp = default(Component2);
            MathTransform swXform = default(MathTransform);
            MathUtility swMathUtil = default(MathUtility);
            bool status = false;
            double[] nPts = new double[3];
            System.DateTime nNow;
            int i = 0;
            bool noCollision = false;
            int direction;
            int dimension = 0;
            double stepsize = clearance / 10.0;
            int t;

            //object[] allComponents2 = (object[])swAssembly.GetComponents(false);
            //object[] allComponents2 = ;


            // Some debugging stuff..
            /*
            for (t=0; t <= swAssembly.GetVisibleComponentsInView().GetUpperBound(0); t++)
            {
                Component2 comp = (Component2)swAssembly.GetVisibleComponentsInView()[t];

                Debug.WriteLine("One of the components!-------------");
                Debug.WriteLine(comp.Name2);
                
                Debug.WriteLine(comp.Solving);
                Debug.WriteLine(comp.ComponentReference);
                Debug.WriteLine(comp.IsGraphicsOnly);
                Debug.WriteLine(comp.IsSpeedPak);
                Debug.WriteLine(comp.IsVirtual);
                Debug.WriteLine(comp.PresentationTransform);
                Debug.WriteLine(comp.ReferencedDisplayState2);
                Debug.WriteLine(comp.ReferencedConfiguration);
                Debug.WriteLine(comp.IsVirtual);
                
            }

            */

            
            // Descriptions for the directions
            Dictionary<int, string> constraint_desc = new Dictionary<int, string>(){
                                                                    {9, "positive x"},
                                                                    {-9, "negative x"},
                                                                {10, "positive y"},
                                                                {-10, "negative y"},
                                                                {11, "positive z"},
                                                                {-11, "negative z"},};

            // Output array
            int[] constraints = new int[6];

            swModelDocExt = (ModelDocExtension)Document.Extension;
            status = swModelDocExt.SelectByID2(moveID, "COMPONENT", 0, 0, 0, false, 0, null, 0);
            //swModelDocExt.SelectByID2(staticID, "COMPONENT", 0, 0, 0, false, 0, null, 0);

            swDragOp = (DragOperator)swAssembly.GetDragOperator();
            swSelMgr = (SelectionMgr)Document.SelectionManager;
            swComp = (Component2)swSelMgr.GetSelectedObjectsComponent2(1);
            swMathUtil = (MathUtility)swApp.GetMathUtility();


            double[] TransformData = new double[16];
            object TransformDataVariant;

            MathTransform orginalTransform = swComp.Transform2;
            

            // Perform the transform along all directions
            foreach (int constraint in constraint_desc.Keys)
            {

                // Setting transformation data
                direction = Math.Sign(constraint);
                int index = constraint * direction;
                Array.Clear(TransformData, 0, TransformData.Length);
                TransformData[index] = stepsize * direction;
                TransformDataVariant = TransformData;

                swXform = (MathTransform)swMathUtil.CreateTransform((TransformDataVariant));

                noCollision = swDragOp.AddComponent(swComp, false);

                
                swDragOp.DynamicClearanceEnabled = false;
                swDragOp.CollisionDetectionEnabled = true;

                swDragOp.CollisionDetection(swAssembly.GetVisibleComponentsInView(), true, true);

                // Translation type transform
                swDragOp.TransformType = 0;

                // Solve by relaxation
                swDragOp.DragMode = 2;

                noCollision = swDragOp.BeginDrag();


                

                for (i = 0; i <= 10; i++)
                {
                    // Returns false if drag fails
                    noCollision = swDragOp.Drag(swXform);
                    
                    if (noCollision == false) { break; };

                    // Animate the drag slowly by setting this condition true
                    if (true)
                    {
                        nNow = System.DateTime.Now;
                        while (System.DateTime.Now < nNow.AddSeconds(.05))
                        {
                            // Process event loop
                            System.Windows.Forms.Application.DoEvents();
                        }
                    }
                        
                }

                Debug.WriteLine(constraint_desc[constraint] + " freedom was " + noCollision.ToString());

                // Add result to output array
                if (noCollision == false)
                {
                    constraints[dimension] = 1;
                } else
                {
                    constraints[dimension] = 0;
                }
                dimension++;

                noCollision = swDragOp.EndDrag();

                swComp.Transform2 = orginalTransform;
                

            }
          

            return constraints;

        }

        // Snap an image of the components for the visualization
        internal static string captureImage(ModelDoc2 Document, string path)
        {
            string guid = Guid.NewGuid().ToString();

            string captureFileName = $"{guid}.bmp";
            string captureFilePath = Path.Combine(path, captureFileName);

            // Save as bitmap and use current window size 
            Document.ViewZoomtofit2();
            Document.SaveBMP(captureFilePath, 300, 300);

            return captureFilePath;
        }

        // Compute the constraints between two components by moving component with ID: "moveID" relative to component with ID: "static ID" using the Collision Detection Manager
        private static int[] computeConstraints1(AssemblyDoc swAssembly, ModelDoc2 Document, SldWorks swApp, string moveID, string staticID, double clearance, object[] allComponents)
        {
            CollisionDetectionManager cdm;
            CollisionDetectionGroup cdg1;
            CollisionDetectionGroup cdg2;
            double[] TransformData = new double[16];
            object TransformDataVariant;
            Component2[] comp1 = new Component2[1];
            Component2[] comp2 = new Component2[1];
            MathTransform[] transform = new MathTransform[1];
            MathTransform[] transform1 = new MathTransform[1];
            MathUtility swMathUtil;
            bool boolstatus;
            int longstatus;
            int direction;
            int i = 0;
            swMathUtil = (MathUtility)swApp.GetMathUtility();

            // Descriptions for the directions
            Dictionary<int, string> constraint_desc = new Dictionary<int, string>(){
                                                                    {9, "positive x"},
                                                                    {-9, "negative x"},
                                                                {10, "positive y"},
                                                                {-10, "negative y"},
                                                                {11, "positive z"},
                                                                {-11, "negative z"},};

            // Output array
            int[] constraints = new int[6];

            // Get collision detection manager
            cdm = (CollisionDetectionManager)swApp.GetCollisionDetectionManager();
            longstatus = cdm.SetAssembly(swAssembly);
            cdm.GraphicsRedrawEnabled = true;

            // Define collision detection groups
            cdg1 = (CollisionDetectionGroup)cdm.CreateGroup();
            cdg2 = (CollisionDetectionGroup)cdm.CreateGroup();


            // Selecting moving component
            boolstatus = Document.Extension.SelectByID2(moveID, "COMPONENT", 0, 0, 0, true, 0, null, 0);
            // Selecting static component
            boolstatus = Document.Extension.SelectByID2(staticID, "COMPONENT", 0, 0, 0, true, 0, null, 0);


            // Use components for collision detection.
            comp1[0] = (Component2)((SelectionMgr)(Document.SelectionManager)).GetSelectedObjectsComponent4(1, -1);
            comp2[0] = (Component2)((SelectionMgr)(Document.SelectionManager)).GetSelectedObjectsComponent4(2, -1);
            longstatus = cdg1.SetComponents(comp1); // Moving component
            longstatus = cdg2.SetComponents(comp2); // Static component


            //MessageBox.Show("checking new component. Current group count: " + cdm.GetGroupCount());

            // Perform the transform along all directions
            foreach (int constraint in constraint_desc.Keys)
            {

                // Setting transformation data
                direction = Math.Sign(constraint);
                int index = constraint*direction;
                Array.Clear(TransformData, 0, TransformData.Length);
                TransformData[index] = clearance * direction;
                TransformDataVariant = TransformData;

                // Create transform
                transform[0] = (MathTransform)swMathUtil.CreateTransform((TransformDataVariant));

                // Apply transform to collision detection groups
                longstatus = cdg1.ApplyTransforms(transform);
                //Debug.WriteLine("Waiting");
                //Thread.Sleep(milliseconds);

                // Check if collisions are present
                int milliseconds = 300;
                Thread.Sleep(milliseconds);
                longstatus = cdm.IsCollisionPresent(false);
                if (longstatus == 1)
                {
                    //Debug.WriteLine("Detected collision in the " + constraint_desc[constraint] + " direction.");
                }

                /*
                object col;
                int nCol = cdm.GetCollisions(false, out col);
                object[] collisions;
                collisions = (object[])col;
                object[] comps;
                Collision aCollision;

                MessageBox.Show("Collision is " + longstatus + " in the " + constraint_desc[constraint] + " and there are: " + nCol);
                Debug.WriteLine("Along " + constraint_desc[constraint]);
                for (int c = 0; c <= nCol-1; c++)
                {
                    Debug.Print("Collision " + (c + 1));
                    aCollision = (Collision)collisions[c];
                    Debug.Print("  Is penetrating? " + aCollision.IsPenetrating());
                    comps = (object[])aCollision.GetComponents();
                    for (int j = 0; j <= comps.GetUpperBound(0); j++)
                    {
                        Debug.Print("   " + ((Component2)comps[j]).Name);
                    }
                }
                Debug.WriteLine("  ");
                */

                
                // Remove the transforms again
                cdg1.RemoveAllTransforms();


                // Add result to output array
                constraints[i] = longstatus;
                i++;


            }
            suppressAllBut([moveID, staticID], allComponents, true);
            // Clear selections and groups
            cdm.RemoveGroup(0);
            cdm.RemoveGroup(0);
            Document.ClearSelection2(true);
           
            return constraints;
        }

        // Special print function to only print when second argument is true
        // Turns out it existed already: Debug.WriteIf()
        
        internal static bool conditionalPrint(string text, bool print)
        {
            if (print)
            {
                Debug.WriteLine(text);
                return true;
            }
   
            return false;
        }

        // The essence of the application happens here.
        internal static DataTable processModel(AssemblyDoc swAssembly, SldWorks swApp, string imageDirectory)
        {

            Component2 comp;

            // Getting active doc and collision detection manager
            ModelDoc2 Document = (ModelDoc2)swApp.ActiveDoc;



            // Dissolving assembly
            dissolveAssembly(swAssembly, Document, true);


            // Get the new components after they were dissolved and unfixed
            object[] allComponents = getAllComponents(swAssembly, true);

            // Clean up component names
            
            swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefUpdateCompNames, false);
            foreach (object component in allComponents)
            {
                comp = (Component2)component;
                string pattern = @"Copy of |(\^.*?)$";
                string formattedName = Regex.Replace(comp.Name2, pattern, "");
                comp.Name2 = formattedName;
            }
            

            Debug.WriteLine("");
            Debug.WriteLine("Cleaning up names..");
            Debug.WriteLine("");

            

            // Get array of all unique interference pairs
            string[][] allInterferences = detectInterferences(swAssembly, Document, true);
            if (allInterferences == null) { return null; }
            allInterferences = getUniqueInter(allInterferences);

            

            Debug.WriteLine("");


            // Compute the constraints and show in debugger.
            // This was used before the constraints where displayed in the graphical interface

            /*
            // Compute and print constraints for all identified interfaces
            Debug.WriteLine("---------- Constraint Analysis ----------");
            Debug.WriteLine("");
            
            foreach (string[] pair in  allInterferences)
            {

                Debug.WriteLine("   Checking constraints: ");
                Debug.WriteLine("       comp1  -  " + cleanUpName(pair[0]));
                Debug.WriteLine("       comp2  -  " + cleanUpName(pair[1]));
                int[] resultCons = computeConstraints1(swAssembly, Document, swApp, pair[0], pair[1], 0.0001);
                Debug.WriteLine("   Constraints (comp 1 is constrained by comp 2 in..)");
                Debug.WriteLine("   +x  | -x  | +y  | -y  | +z | -z ");
                Debug.WriteLine("  __________________________________");
                
                Debug.WriteLine("    " + string.Join("  |  ", resultCons));
                Debug.WriteLine("");

            }
            */

            // Create a table for the constraint data
            DataTable dataTable = new DataTable();
            string[] headers = ["Interface", "+x", "-x", "+y", "-y", "+z", "-z","imagePath"];


            // Add columns to the DataTable. These are the interfaces.
            for (int col = 0; col < 8; col++)
            {
                DataColumn dataColumn = new DataColumn(headers[col], typeof(string));
                dataColumn.ReadOnly = true; // Set the column as read-only
                dataTable.Columns.Add(dataColumn);
            }

            // Add rows to the data table. These are the constraints.
            for (int row = 0; row < allInterferences.Length; row++)
            {
                // The two components in question
                string comp1 = allInterferences[row][0];
                string comp2 = allInterferences[row][1];

                // Hide all components but the ones in question
                suppressAllBut([comp1, comp2], allComponents, false);

                // Snap an image for the visualization
                string imagePath = captureImage(Document, imageDirectory);

                // Compute the constraints. Choose method 1 or 2
                //int[] constraints = computeConstraints2(swAssembly, Document, swApp, comp1, comp2, 0.001, allComponents);
                int[] constraints = computeConstraints1(swAssembly, Document, swApp, comp1, comp2, 0.0001, allComponents);

                // Show all components again
                suppressAllBut([comp1, comp2], allComponents, true);


                // Add constraints to rows
                DataRow dataRow = dataTable.NewRow();
                for (int col = 0; col < 8; col++)
                {
                    if (col == 0) { dataRow[col] = cleanUpName(comp1) + " - " + cleanUpName(comp2); }
                    else if (col !=7) {
                        dataRow[col] = constraints[col - 1];
                    } else
                    {
                        dataRow[col] = imagePath;
                    }
                }
                dataTable.Rows.Add(dataRow);
            }

            return dataTable;

        }


        // Used to create dictionary for the image paths
        internal static Dictionary<string,string> createDictFromTable(DataTable dataTable, string imagePath)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();

            dictionary["Full assembly"] = imagePath;

            foreach (DataRow row in dataTable.Rows)
            {
                string key = Convert.ToString(row[0]);
                string value = Convert.ToString(row[7]);
                dictionary[key] = value;
            }

            return dictionary;
        }
 

        // Return only the unique interferences from interferences array
        internal static string[][] getUniqueInter(string[][] allInterferences)
        {

            // Create a HashSet to store unique pairs
            HashSet<string> uniquePairs = new HashSet<string>();

            foreach (var pair in allInterferences)
            {
                // Sort the elements within each pair alphabetically and concatenate
                var sortedPair = string.Join(", ", pair.OrderBy(s => s));

                uniquePairs.Add(sortedPair);
               
            }

            // Convert back to an array
            string[][] uniquePairsArray = uniquePairs.Select(sortedPair => sortedPair.Split(new string[] { ", " }, StringSplitOptions.None)).Select(pair => new string[] { pair[0], pair[1] }).ToArray();


            return uniquePairsArray;
        }

        // Dissolve model into multiple components
        internal static bool dissolveAssembly(AssemblyDoc swAssembly, ModelDoc2 Document, bool showProgress)
        {
            // Get top component
            object[] topComponents = (object[])swAssembly.GetComponents(false);

            // Dissolve into multiple
            Component2 component = (Component2)topComponents[0];
            Debug.WriteLine("Dissolving " + component.Name2);
            Feature feat = swAssembly.FeatureByName(component.Name2);
            feat.BreakLink(false, true);
            Document.Extension.SelectAll();
            swAssembly.DissolveSubAssembly();

            int k = 0;
            while (dissolvingRound(swAssembly, Document, showProgress) > 0 && k < 50)
            {
                k++;
            }

            // Unfixing all components
            Document.Extension.SelectAll();
            swAssembly.UnfixComponent();
            return true;



        }

        // Another round of dissolving. Returns the number of components that are still subassemblies, so the function may be called again until all is dissolved
        internal static int dissolvingRound(AssemblyDoc swAssembly, ModelDoc2 Document, bool showProgress)
        {
            // Get all components
            object[] allComponents = (object[])swAssembly.GetComponents(true);
            Component2 comp = default(Component2);
            Feature feat;
            int j;
            int total = 0;

            for (j = 0; j <= allComponents.GetUpperBound(0); j++)
            {
                comp = (Component2)allComponents[j];

                int c = comp.IGetChildrenCount();
                
                if (c > 0)
                {
                    total++;

                    feat = swAssembly.FeatureByName(comp.Name2);
                    feat.BreakLink(false, true);
                    string id = comp.GetSelectByIDString();
                    Document.Extension.SelectByID(id, "COMPONENT", 0, 0, 0, false, 0, null);
                    swAssembly.DissolveSubAssembly();
                }
            }

            return total;
        }

        // Remove references to parent components in ID
        internal static string cleanUpID(string ID)
        {
            string result;
            int index = ID.IndexOf('/');
            if (index >= 0)
            {
                result = ID.Substring(0, index);
            }
            else
            {
                result = ID;
            }

            return result;
        }

        // Remove copy number and references to parent components in name
        static string cleanUpName(string input)
        {
            int index = input.IndexOf("^");
            if (index >= 0)
            {
                // Return the substring starting from the symbol
                input = input.Substring(0, index);
            }
            else
            {
                // Return an empty string if the symbol is not found
                return input;
            }
            index = input.IndexOf("_");
            if (index >= 0)
            {
                // Return the substring starting from the symbol
                input = input.Substring(0, index);
                return input;
            }
            else
            {
                // Return an empty string if the symbol is not found
                return input;
            }
        }


        // Suppress all components by the two selected. If unsuppress is true, it will show all components again
        internal static bool suppressAllBut(string[] keepCleanID, object[] allComponents, bool unsuppress)
        {
            int j = 0;
            int state;
            Component2 comp;

            if (unsuppress == true)
            {
                state = 2;
            } else
            {
                state = 0;
            }

            for (j = 0; j <= allComponents.GetUpperBound(0); j++)
            {
                comp = (Component2)allComponents[j];

                if (keepCleanID.Contains(cleanUpID(comp.GetSelectByIDString()))) {
                    continue;
                }
                comp.SetSuppression2(state);
            }
            return true;
        }
        

        
        // Get all components in assembly. Print overview if print == true
        internal static object[] getAllComponents(AssemblyDoc swAssembly, bool print)
        {

            // Get all components
            object[] allComponents = (object[])swAssembly.GetComponents(true);
            Component2 comp = default(Component2);
            int j;


            // Print components
            if (print)
            {
                Debug.WriteLine("---------- All Components ----------");
                Debug.WriteLine("");
                for (j = 0; j <= allComponents.GetUpperBound(0); j++)
                {
                    comp = (Component2)allComponents[j];
                    Debug.WriteLine("  " + cleanUpName(comp.Name2));
                    Debug.WriteLine("  Full name -  " + comp.Name2);
                    Debug.WriteLine("         ID -  " + comp.GetSelectByIDString());
                    Debug.WriteLine("");
                }
                Debug.WriteLine("");
            }
            
            // Return all components
            return allComponents;
        }

        // Print an overview of all bounding boxes of all components in the assembly
        internal static bool printBoundingBoxes(AssemblyDoc swAssembly)
        {
            object[] vComponents = null;
            Component2 oneComponent = default(Component2);

            int i;
            object Box = null;
            double[] BoxArray = new double[6];

            // Get the components in the assembly and print their bounding boxes
            vComponents = (object[])swAssembly.GetComponents(false);
            for (i = 0; i <= vComponents.Length - 1; i++)
            {
                oneComponent = (Component2)vComponents[i];
                Box = (object)oneComponent.GetBox(false, false);
                BoxArray = (double[])Box;
                Debug.WriteLine("Component name: " + oneComponent.Name2);
                Debug.WriteLine("Bounding box:");
                Debug.Write("x1: " + Math.Round(BoxArray[0]*1000,3) + " mm ");
                Debug.Write("y1: " + Math.Round(BoxArray[1] * 1000, 3) + " mm ");
                Debug.WriteLine("z1: " + Math.Round(BoxArray[2] * 1000, 3) + " mm ");
                Debug.Write("x2: " + Math.Round(BoxArray[3] * 1000, 3) + " mm ");
                Debug.Write("y2: " + Math.Round(BoxArray[4] * 1000, 3) + " mm ");
                Debug.WriteLine("z3: " + Math.Round(BoxArray[5] * 1000, 3) + " mm ");
                Debug.WriteLine(" ");

            }

            return true;
        }

    }
}
