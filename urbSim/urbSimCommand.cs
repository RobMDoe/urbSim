using System;
using System.Collections.Generic;

using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.Collections;

namespace urbSim
{
    public class urbSimCommand : Command
    {
        public urbSimCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a reference in a static property.
            Instance = this;
        }
        //The only instance of this command.</summary>
        public static urbSimCommand Instance
        {
            get; private set;
        }
        //The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName
        {
            get { return "urbSim"; }
        }

        //typing 'urbSim' in Rhino runs this command
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            RhinoDoc.ActiveDoc.Views.RedrawEnabled = false;

            RhinoApp.WriteLine("The urbSim has begun.");

            //all the functionality that follows is saved into this object 'theUrbanModel'
            urbanModel theUrbanModel = new urbanModel();

            int halfMaxRoadWidth = 6;

            if (!getPrecinct(theUrbanModel))           //these if statements call the functions below
                return Result.Failure;

            if (!generateRoadNetwork(theUrbanModel, 3, halfMaxRoadWidth))    //Only progresses if roads generated; ints = half min/max road widths
                return Result.Failure;

            if (!createBlocks(theUrbanModel, halfMaxRoadWidth)) //int = half max road width 
                return Result.Failure;

            if (!subdivideBlocks(theUrbanModel, 45, 25))
                return Result.Failure;

            RhinoApp.WriteLine("The urbSim is complete.");

            RhinoDoc.ActiveDoc.Views.RedrawEnabled = true;

            return Result.Success;
        }

        //Ask user to select a surface representing a precinct 
        public bool getPrecinct(urbanModel model)
        {
            GetObject obj = new GetObject();
            obj.GeometryFilter = ObjectType.Surface;
            obj.SetCommandPrompt("Please select a surface representing your precinct.");

            GetResult res = obj.Get();

            if (res != GetResult.Object)
            {
                RhinoApp.WriteLine("User failed to select a surface.");
                return false;
            }

            if (obj.ObjectCount == 1)
                model.precinctSrf = obj.Object(0).Surface();

            return true;
        }

        //Using the precint, generate a road network
        public bool generateRoadNetwork(urbanModel model, int halfMinRoadWidth, int halfMaxRoadWidth)
        {
            int noIterations = 6;

            Random rndRoadT = new Random(); // a random generator

            List<Curve> obstCrvs = new List<Curve>(); //list of curves

            //separate list of offset curves 
            List<Curve> offCrvs = new List<Curve>();

            //Extract the border from the precinct surface - temp geometry
            Curve[] borderCrvs = model.precinctSrf.ToBrep().DuplicateNakedEdgeCurves(true, false);

            foreach (Curve itCrv in borderCrvs) //borderCrvs saved into obstCrvs
                obstCrvs.Add(itCrv);

            if (borderCrvs.Length > 0)
            {
                int noBorders = borderCrvs.Length;

                Random rnd = new Random();
                Curve theCrv = borderCrvs[rnd.Next(noBorders)]; //selects one border curve

                recursivePerpLine(theCrv, ref obstCrvs, ref offCrvs, rndRoadT, -1, noIterations, halfMinRoadWidth, halfMaxRoadWidth); //call to function below; //////dir -1 gives better results (reverses XAxis direction)
            }

            model.roadNetwork = obstCrvs; // obstacle curves list stored in urbanModel (these get in the way of extension lines)

            foreach (Curve offCrv in offCrvs) // add kerb lines by ref from method below to road curves list in urbanModel
                obstCrvs.Add(offCrv);

            if (obstCrvs.Count > borderCrvs.Length)
                return true;
            else
                return false;
        }

        public bool recursivePerpLine(Curve inpCrv, ref List<Curve> inpObst, ref List<Curve> offCrvs, Random inpRnd, int dir, int cnt, int hMinRW, int hMaxRW)
        //inpObst is a list of obstacle curves which the extended line hits
        {
            if (cnt < 1)
                return false;

            //select a random point on a curve
            double t = inpRnd.Next(20, 80) / 100.0;      //instead of random point anywhere on curve
            Plane perpFrm;

            Point3d pt = inpCrv.PointAtNormalizedLength(t);
            inpCrv.PerpendicularFrameAt(t, out perpFrm); //'out perpFrm' assigns to the variable perpFrm
            Point3d pt2 = Point3d.Add(pt, perpFrm.XAxis * dir);

            //Draw a line perpendicular
            Line ln = new Line(pt, pt2);
            Curve lnExt = ln.ToNurbsCurve().ExtendByLine(CurveEnd.End, inpObst); //lnExt extended to all border curves in inObst (4 boundaries plus new lnExt)

            if (lnExt == null)
                return false;       //deals with problem along one curve edge

            inpObst.Add(lnExt); //first line added as new extended line to inpObst, and made available by reference in obstCrvs

            //////////// ALL ADDED BELOW
            
            // make heirarchy roads: road type 1 = 12m wide; road type 2 = 8m wide; road type 3 = 6m wide
            Random halfRoad = new Random();
            int hfR = halfRoad.Next(hMinRW, hMaxRW);

            //make offset points
            Point3d lnExtCrvStart = lnExt.PointAtStart;
            Plane plnPerpOne;
            double p;           ///p = 0, at start of curve domain
            lnExt.ClosestPoint(lnExtCrvStart, out p);
            lnExt.PerpendicularFrameAt(p, out plnPerpOne);
            Point3d ptR = Point3d.Add(lnExtCrvStart, plnPerpOne.XAxis * hfR);
            Point3d ptL = Point3d.Add(lnExtCrvStart, plnPerpOne.XAxis * -hfR);

            //make points, then lines extended to boundary curves only
            Point3d ptR2 = Point3d.Add(ptR, plnPerpOne.ZAxis * Math.Abs(dir));
            Point3d ptL2 = Point3d.Add(ptL, plnPerpOne.ZAxis * Math.Abs(dir));
            Line offLnExt1 = new Line(ptR, ptR2);
            Line offLnExt2 = new Line(ptL, ptL2);
            Curve offCrv1 = offLnExt1.ToNurbsCurve().ExtendByLine(CurveEnd.End, inpObst);
            Curve offCrv2 = offLnExt2.ToNurbsCurve().ExtendByLine(CurveEnd.End, inpObst);

            //add offset curves to a separate list of curves
            offCrvs.Add(offCrv1);
            offCrvs.Add(offCrv2);

            //RhinoDoc.ActiveDoc.Objects.AddLine(lnExt.PointAtStart, lnExt.PointAtEnd); //only lnExt is drawn, the new extended lines, not the 4 border curves
            //RhinoDoc.ActiveDoc.Objects.AddCurve(offCrv1);
            //RhinoDoc.ActiveDoc.Objects.AddCurve(offCrv2);
            //RhinoDoc.ActiveDoc.Views.Redraw();

            ////////////// ALL ADDED ABOVE

            recursivePerpLine(lnExt, ref inpObst, ref offCrvs, inpRnd, 1, cnt - 1, hMinRW, hMaxRW); //lnExt hits obstructions
            recursivePerpLine(lnExt, ref inpObst, ref offCrvs, inpRnd, -1, cnt - 1, hMinRW, hMaxRW);

            return true;
        }

        public bool createBlocks(urbanModel model, int hMaxRW)
        {
            Random blockType = new Random();

            Brep precintPolySurface = model.precinctSrf.ToBrep().Faces[0].Split(model.roadNetwork, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);     //split face using 3D trimming curves

            List<Brep> allBlocks = new List<Brep>(); // keep as list in createBlocks only

            foreach (BrepFace itBF in precintPolySurface.Faces)
            {
                Brep itBlock = itBF.DuplicateFace(false); // a collection of Brep faces
                itBlock.Faces.ShrinkFaces();
                itBlock.Flip();
                allBlocks.Add(itBlock); // all blocks, including road surfaces/blocks which don't need saving, so temporary
            }
            ///////// ALL ADDED BELOW

            List<block> blocks = new List<block>(); // assign to model.blocks - plots will also be assigned to these blocks

            //max road width as integer;
            int halfR = 2 * hMaxRW;

            foreach (Brep itB in allBlocks) //save only blocks (to bldBlocks) wider than the widest road (i.e. 2*halfRoad) to list, i.e. exclude road surfaces
            {
                if (itB.Edges[0].GetLength() > 2 * halfR && itB.Edges[1].GetLength() > 2 * halfR && itB.Edges[2].GetLength() > 2 * halfR && itB.Edges[3].GetLength() > 2 * halfR)
                {
                    int theBlockType = blockType.Next(4); //the random type chosen, up to integer 4
                    blocks.Add(new block(itB, theBlockType)); // assigns to block method in urbanModel, taking itB input and theBlockType from the random integer

                    ObjectAttributes oaPk = new ObjectAttributes(); //makes type 0 parks plus green surface
                    oaPk.ColorSource = ObjectColorSource.ColorFromObject;
                    oaPk.ObjectColor = System.Drawing.Color.FromArgb(0, 153, 76);
                    Extrusion parkExt = new Extrusion();
                    if (theBlockType == 0)          
                    {
                        Curve boundary = Curve.JoinCurves(itB.DuplicateNakedEdgeCurves(true, false))[0];
                        parkExt = Extrusion.Create(boundary, 0.1, true);
                        RhinoDoc.ActiveDoc.Objects.AddExtrusion(parkExt, oaPk);
                    }

                    RhinoDoc.ActiveDoc.Objects.AddBrep(itB);
                    RhinoDoc.ActiveDoc.Views.Redraw();
                }
            }

            /////////   ALL ADDED ABOVE

            if (blocks.Count > 0)
            {
                model.blocks = blocks; //assigns blocks to to model.blocks in urbanModel

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool subdivideBlocks(urbanModel model, int minPlotDepth, int maxPlotWidth)
        {
            //check dimensions, find shorter dim, validate if it needs to be subdivided, (if minPLotDepth can be achieved, depth > minPlotDepth * 2)
            //if so subdivide halfway, then into smaller plots based on maxPlot width
            
            foreach (block itBlock in model.blocks)
            {
                Brep itSrf = itBlock.blockSrf; // to get itBlock surface as Brep (use class or object outside this class urbSim, by accessing from and assigning to fields / properties / attributes)
                itBlock.plots = new List<plot>(); //so this is where plots are linked to block; plots assigned to block object, not to model object

                Curve[] blockBorderCrvs = itSrf.DuplicateNakedEdgeCurves(true, false); //border curves of each itBlock

                List<Curve> splitLines = new List<Curve>();

                itSrf.Faces[0].SetDomain(0, new Interval(0, 1)); //reparameterise surface U and V, so domains 0 - 1
                itSrf.Faces[0].SetDomain(1, new Interval(0, 1));

                Point3d pt1 = itSrf.Faces[0].PointAt(0, 0);
                Point3d pt2 = itSrf.Faces[0].PointAt(0, 1);
                Point3d pt3 = itSrf.Faces[0].PointAt(1, 1);
                Point3d pt4 = itSrf.Faces[0].PointAt(1, 0);

                double length = pt1.DistanceTo(pt2);
                double width = pt1.DistanceTo(pt4);

                Point3d sdPt1 = new Point3d();
                Point3d sdPt2 = new Point3d();

                if (length > width)
                {
                    if (width > (minPlotDepth * 2)) //suitable for subdivision
                    {
                        //create a subdividing line
                        sdPt1 = itSrf.Faces[0].PointAt(0.5, 0); //
                        sdPt2 = itSrf.Faces[0].PointAt(0.5, 1);
                    }
                }
                else //if width is wider
                {
                    if (length > (minPlotDepth * 2))
                    {
                        sdPt1 = itSrf.Faces[0].PointAt(0, 0.5);
                        sdPt2 = itSrf.Faces[0].PointAt(1, 0.5);
                    }
                }

                Line subDLine = new Line(sdPt1, sdPt2);
                Curve subDCrv = subDLine.ToNurbsCurve();

                splitLines.Add(subDCrv);

                double crvLength = subDCrv.GetLength();
                double noPlots = Math.Floor(crvLength / maxPlotWidth);

                for (int t = 0; t < noPlots; t++)
                {
                    double tVal = t * 1 / noPlots; //t is 0, 0.2, 0.4 ... 1

                    Plane perpFrm;

                    Point3d evalPt = subDCrv.PointAtNormalizedLength(tVal);
                    subDCrv.PerpendicularFrameAt(tVal, out perpFrm);

                    //inpCrv.PerpendicularFrameAt(t, out perpFrm) 'out perpFrm' assigns to the variable perpFrm

                    Point3d ptPer2Up = Point3d.Add(evalPt, perpFrm.XAxis);
                    Point3d ptPer2Down = Point3d.Add(evalPt, -perpFrm.XAxis);

                    //Draw a line perpendicular
                    Line ln1 = new Line(evalPt, ptPer2Up);
                    Line ln2 = new Line(evalPt, ptPer2Down);

                    Curve lnExt1 = ln1.ToNurbsCurve().ExtendByLine(CurveEnd.End, blockBorderCrvs); //lnExt extended to all border curves in inObst (4 boundaries plus new lnExt)
                    Curve lnExt2 = ln2.ToNurbsCurve().ExtendByLine(CurveEnd.End, blockBorderCrvs);

                    splitLines.Add(lnExt1);
                    splitLines.Add(lnExt2);

                }

                Brep plotPolySurface = itSrf.Faces[0].Split(splitLines, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);  //split face using 3D trimming curves

                foreach (BrepFace itBF in plotPolySurface.Faces)
                {
                    Brep itPlot = itBF.DuplicateFace(false); // a collection of Brep faces
                    itPlot.Faces.ShrinkFaces();
                    itBlock.plots.Add(new plot(itPlot, itBlock.type));  //plots assigned to block object as list of Breps // this syntax, for assigning to a list outside of this class and into urbSim.block.plots
                    //itBlock.type takes integer value from 'block' object and assigns same value to 'plot' object
                    //RhinoDoc.ActiveDoc.Objects.AddBrep(itPlot);
                }

            }

            return true;
        }

    }

}
