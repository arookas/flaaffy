
workspace "flaaffy"
configurations { "Debug", "Release" }
targetdir "bin/%{cfg.buildcfg}"
startproject "mareep"

filter "configurations:Debug"
defines { "DEBUG" }
symbols "on"

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
framework "4.6.1"

links {
	"arookas",
	"System",
	"System.Xml",
	"System.Xml.Linq",
}

files {
	"mareep/**.cs",
}

excludes {
	"mareep/bin/**",
	"mareep/obj/**",
}
