
workspace "flaaffy"
	configurations { "Debug", "Release" }
	targetdir "bin/%{cfg.buildcfg}"
	startproject "mareep"
	
	filter "configurations:Debug"
		defines { "DEBUG" }
		flags { "Symbols" }
	
	filter "configurations:Release"
		defines { "RELEASE" }
		optimize "On"
	
	project "mareep"
		kind "ConsoleApp"
		language "C#"
		namespace "arookas"
		location "mareep"
		entrypoint "arookas.mareep"
		targetname "mareep"
		
		links { "arookas", "System", "System.Xml", "System.Xml.Linq", }
		
		files {
			"mareep/**.cs",
		}
		
		excludes {
			"mareep/bin/**",
			"mareep/obj/**",
		}
	