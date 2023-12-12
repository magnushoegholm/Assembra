using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


// Source:  https://github.com/AmenJlili/Code-repo-for-sw-custom-properties-tool-video-series

namespace Singleton
{
    internal class SolidWorksSingleton
    {
        // private only accessible from class
        // static - does not belong to any instance of the class
        private static SldWorks swApp;

        // is only accessible within class, which mean you can not create
        // the object from outside the class. weird? no. we will create the
        // class using the methods below
        private SolidWorksSingleton()
        {

        }

        // get solidworks
        internal static SldWorks getApplication()
        {
            if (swApp == null)
            {
                swApp = Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application")) as SldWorks;

                return swApp;
            }

            return swApp;
        }

        // get solidworks async
        // it is internal so it can be use from within the assembly
        // static means that it belongs to the class and not the instance
        internal async static Task<SldWorks> getApplicationAsync()
        {
            if (swApp == null)
            {
                return await Task<SldWorks>.Run(() => {
                    swApp = Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application")) as SldWorks;

                    return swApp;
                });
            }

            return swApp;
        }

        // dispose solidworks
        internal static void Dispose()
        {
            if (swApp != null)
            {
                swApp.ExitApp();
                swApp = null;
            }
        }
    }
}