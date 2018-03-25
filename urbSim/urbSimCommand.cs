using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace urbSim
{
    public class urbSimCommand : Command
    {
        public urbSimCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static urbSimCommand Instance
        {
            get; private set;
        }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName
        {
            get { return "urbSim"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // TODO: start here modifying the behaviour of your command.
            // ---
            RhinoApp.WriteLine("The {0} has begun.", EnglishName);

            //getPrecint()              //Ask user to select a surface representing a precint 
            //generateRoadNetwork()     //Using the precint, generate a road network
            //createBlocks()            //Using road network, create blocks
            //subdivideBlocks()         //Subdivide blocks into plots
            //instantiateBuildings()    //Place buildings on each plot
                                  
            RhinoApp.WriteLine("The {0} is complete.", EnglishName);

            // ---

            return Result.Success;
        }
    }
}
