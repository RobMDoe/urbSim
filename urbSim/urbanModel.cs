using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace urbSim
{
    public class urbanModel             //a collection of fields, properties, and a function
    {
        public string name = "Urban Model";
        public Surface precinctSrf;
        public List<Curve> roadNetwork;  // <> = generic collection of 'Curve' and 'Brep' or other variables, strongly typed
        public List<block> blocks; // a list of 'block' objects assigned from 'urbSimCommand.createBlocks'

        //the 'constructor', the initialiser of the project
        public urbanModel()
        {

        }
    }

    public class block //the object defined by the field/property above
    {
        public int type; //0 = park, 1 = low rise, 2 = mid rise, 3 = high rise
        public Brep blockSrf;
        public List<plot> plots; //block.plots links plot objects to block. A list of 'plot' objects, not a list of 'Brep' objects; later, building will become part of the plots (make building object/class)
        
        //add a list of building objects here?

        public block(Brep inpBlkSrf, int inpBlkType) //so, when block.block is called from urbSimCommand.createBlocks those arguments are passed into this method, so that plot class below knows this
        {
            this.blockSrf = inpBlkSrf; //inpBlkSrf assigned to list of blockSrf
            this.type = inpBlkType;
        }

    }

    public class plot
    {
        public Brep plotSrf;
        public Curve buildingOutline;
        public Extrusion buildingExtrusion; // THIS TO GO TO BUILDING CLASS?
        //public List<building> buildings; // list of lists, subdivision of building

        Random bldHeight = new Random();

        List<Brep> allUnits = new List<Brep>(); // still has to be outside iteration to collect buildings and their units in a list of lists

        public plot(Brep inpPltSrf, int inpPlotType) //as createBuilding iterates, plot type is assigned here (assigned from block type)
        {
            this.plotSrf = inpPltSrf; //when plotSrf assigned, then next call made to createBuilding
            this.createBuilding(inpPlotType); //yes, calls method below to create building
        }
                 
        //// CLASS BUILDING FOLLOWS

        public bool createBuilding(int inpPlotType) //ALL TO BECOME BUILDING CLASS? BUT WHY PUT IT HERE AND NOT UNDER URBSIMCOMMAND??

        {
            if (this.plotSrf.GetArea() < 50) //skip areas that are too small
                return false;

            if (inpPlotType > 0)   // skip parks, type 0 (or, green rgb 90,143,41?)
            {
                int minBldHeight = 0;
                int maxBldHeight = 9;

                if (inpPlotType == 1) // low rise
                {
                    minBldHeight = 12;
                    maxBldHeight = 24;
                }
                if (inpPlotType == 2) // mid rise
                {
                    minBldHeight = 36;
                    maxBldHeight = 72;
                }
                if (inpPlotType == 3) // high rise
                {
                    minBldHeight = 84;
                    maxBldHeight = 120;
                }
         
                double actBuildingHeight = this.bldHeight.Next(minBldHeight, maxBldHeight);

                System.Drawing.Color bldCol = System.Drawing.Color.White;

                if(actBuildingHeight < 10)
                    bldCol = System.Drawing.Color.FromArgb(204,0,0);
                else if (actBuildingHeight < 36)
                    bldCol = System.Drawing.Color.FromArgb(204,102,0);
                else if (actBuildingHeight < 50)
                    bldCol = System.Drawing.Color.FromArgb(204,204,0);
                else if (actBuildingHeight < 73)
                    bldCol = System.Drawing.Color.FromArgb(0,204,204);
                else if (actBuildingHeight < 91)
                    bldCol = System.Drawing.Color.FromArgb(0,102,204);
                else if (actBuildingHeight > 91)
                    bldCol = System.Drawing.Color.FromArgb(102,0,204);

                ObjectAttributes oa = new ObjectAttributes();
                oa.ColorSource = ObjectColorSource.ColorFromObject;
                oa.ObjectColor = bldCol;

                Curve border = Curve.JoinCurves(this.plotSrf.DuplicateNakedEdgeCurves(true, false))[0]; //makes first index of list these joined curves i.e. a polyline = border

                this.buildingOutline = Curve.JoinCurves(border.Offset(Plane.WorldXY, -4, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, CurveOffsetCornerStyle.None))[0]; //makes first index of list these offset joined curves i.e.a polyline = buildingOutline

                double maxStoreyHeight = 3.6; //applies to all building types; make different for each type?
                double actStoNo = Math.Floor(actBuildingHeight / maxStoreyHeight); //so 25m at max 3 each gives 8 storeys
                double actstoHt = actBuildingHeight / actStoNo; // so 25m, 8 storeys of 3.125m each

                this.buildingExtrusion = Extrusion.Create(this.buildingOutline, actBuildingHeight, true);

                RhinoDoc.ActiveDoc.Objects.AddExtrusion(this.buildingExtrusion, oa);
                             
                //// CLASS UNIT FOLLOWS

                //extrude first curve of bldgoutline to storey height only
                List<Brep> bUnits = new List<Brep>(); // units for each building collected here

                Brep gUnit; // ground level unit added to list first
                gUnit = Extrusion.Create(this.buildingOutline, actstoHt, true).ToBrep();
                bUnits.Add(gUnit); 
                RhinoDoc.ActiveDoc.Objects.AddBrep(gUnit, oa);

                allUnits.AddRange(bUnits);

                //copy building outline
                Curve newOline = this.buildingOutline;

                for (int t = 0; t < (actStoNo-1); t++) //storeys less 1, because startS level 1, not ground
                {
                    //make a point3d to storey height above and vector
                    Point3d pt1 = newOline.PointAtStart;
                    Plane perpFrm;
                    newOline.PerpendicularFrameAt(0.0, out perpFrm);
                    Point3d pt2 = Point3d.Add(pt1, perpFrm.ZAxis * actstoHt);
                    Vector3d vecD = (pt2 - pt1);
                    //translate curve to storey height above
                    newOline.Translate(vecD);
                    //extrude newOline and make brep
                    Brep upUnit = Extrusion.Create(newOline, actstoHt, true).ToBrep();
                    bUnits.Add(upUnit);

                    RhinoDoc.ActiveDoc.Objects.AddBrep(upUnit, oa);
                }

                // with bUnits collected, add these to a list outside of 

                //this.buildingUnits = allUnits; //assign units to buildings

                //extract data from building and levels

                //link to Excel spreadsheet

                ///// ALL ADDED ABOVE

            }

            return true;

        }

    }
    /* 
        public class building
    {
        public Brep buildingExtrusion; //Brep from extrusion
        public List<unit> units;

        public building(Brep inpEnv) 
        {
            extrude building from buildingOutline
            list buildings, link to plot
        }
    }

    public class unit
    {
        public Brep unit; //Brep extrusion
        
        public unit(buildingOutline);
        {
            extrude buildingOutline to storey height
            list units, link to building
        }

    }
    
    public class data
    {
        public Float area;
        public Float volume;
        public Float roadDistance;
        public Float orientation;

        public area(precinct, block, building, plot, unit)
        {

        }
    }
     
     */
}
