/*-----------------------------------------------------------------------------
 * Copyright (c) DaisukeAtaraxiA. All rights reserved.
 * Licensed under the MIT License.
 * See LICENSE.txt in the project root for license information.
 *---------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.VCProjectEngine;

namespace VSslnToCMake
{
    static class Constants
    {
        public const string CMAKE_REQUIRED_VERSION = "3.12.2";
    }

#if (VS2015)
    public class ConverterVs2015 : AbstractConverter
#elif (VS2017)
    public class ConverterVs2017 : AbstractConverter
#endif
    {
#if (VS2015)
        public ConverterVs2015()
#elif (VS2017)
        public ConverterVs2017()
#endif
        {
            Platform = "x64";
        }

        public override bool Convert(EnvDTE.DTE dte)
        {
            List<VCProjectInfo> prjInfoList = VCProjectInfo.MakeList(dte,TargetConfigurations, Platform);
            if (prjInfoList.Count == 0) return false;

            logger.Info("Converting the projects");

            var solutionDir = Path.GetDirectoryName(dte.Solution.FullName);
            bool prjDirIsSlnDir = false;
            var cmProjects = new List<CMProject>();

            foreach (var prjInfo in prjInfoList)
            {
                var cmProject = new CMProject(prjInfo);
                cmProject.setLogger(logger);
                if (!cmProject.Prepare())
                {
                    return false;
                }
                cmProjects.Add(cmProject);

                if (!prjDirIsSlnDir &&
                    Utility.NormalizePath(cmProject.CmakeListsDir) == Utility.NormalizePath(solutionDir))
                {
                    prjDirIsSlnDir = true;
                }
            }

            foreach (var cmProject in cmProjects)
            {
                if (!cmProject.Convert(solutionDir, cmProjects))
                {
                    return false;
                }
            }

            logger.Info("Converting the projects - done");

            // Output CMakeLists.txt for the solution file.
            var cmakeListsPath = Path.Combine(solutionDir, "CMakeLists.txt");
            if (!prjDirIsSlnDir && Utility.FileCanOverwrite(cmakeListsPath))
            {
                logger.Info("Converting the solution");
                logger.Info($"--- Converting {dte.Solution.FullName} ---");
                var sw = new StreamWriter(cmakeListsPath);
                sw.WriteLine($"cmake_minimum_required(VERSION {Constants.CMAKE_REQUIRED_VERSION})");
                sw.WriteLine();
                sw.WriteLine("project({0})",
                             Path.GetFileNameWithoutExtension(
                                 dte.Solution.FileName));
                sw.WriteLine();
                foreach (var cmProject in cmProjects)
                {
                    var relativePath = Utility.ToRelativePath(cmProject.CmakeListsDir, solutionDir);
                    sw.WriteLine($"add_subdirectory({relativePath})");
                }

                sw.Close();
                logger.Info($"  {Path.GetFileNameWithoutExtension(dte.Solution.FullName)} -> {cmakeListsPath}");
                logger.Info("Converting the solution - done");
            }
            
            return true;
        }
    }
}
