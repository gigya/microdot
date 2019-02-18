<Query Kind="Statements">
  <NuGetReference>Gigya.Build.Solo</NuGetReference>
  <Namespace>Gigya.Build.Solo</Namespace>
  <Namespace>Gigya.Build.Solo.Command</Namespace>
</Query>

//
// Run, to stop visual studio complile projects again and again.
// 1.0.2
// A.Chirlin, 2019/02/16
//
// Since upgrade to 4.7.2 there is negative impact on compilation time, VS recompiles again and again the projects despite absence of changes.
// Such behavior is result of facade (design time references) not actually copied to bin, but recognized as missing and triggering rebuild.
// More about how I found this behavior in: https://michaelscodingspot.com/2017/04/28/visual-studio-keeps-rebuilding-projects-no-good-reason/
//
// Why script?
// Every time you running paket install, ooops the properties will be rewritten.
//

var scriptDir = Path.GetDirectoryName(Util.CurrentQueryPath);
var filesTxt = Path.GetFileNameWithoutExtension(Util.CurrentQueryPath) + ".txt";
var slnFile = new FileInfo(Path.Combine(scriptDir, "..", "Microdot.sln")).FullName;

var s = new Solution(slnFile);
var designTime = @"c:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades";
var affected = new List<string>();
var exclude = new[]{"System.ValueTuple", "System.ComponentModel.Annotations"}; // I paid attention, for some reason these are still copied, despite is it design time

foreach (var project in s.Projects.OrderBy(p=>p.FullName))
{
	// load project XML
	var xmlDoc = new XmlDocument();
	var projectFile = project.FullName;
	xmlDoc.Load(projectFile);
	var ns = xmlDoc.DocumentElement.Attributes["xmlns"].InnerText;
	var mgr = new XmlNamespaceManager(xmlDoc.NameTable);
	mgr.AddNamespace("a", ns);

	// Find all references with Private = True (meaning copy local true).
	// If this reference in design time folder, meaning msbuild won't copy it despite copy local true AND it causing rebuild (as not found in output folder).
	// Such reference apparently has no meaning of copy local and we can convert to False.
	// I found few exceptions from rule above, that despite the reference resides in design time (facade) folder, it is still copied by msbuild. Why? It is same name and public key token ...
	// So far, I narrowed the assemblies to System.* nuget packages and installed by paket only.
	var hasChanges = false;
	foreach (XmlAttribute element in xmlDoc.SelectNodes(@"//a:Reference[a:Private[.='True'] and a:Paket[.='True']]/@Include[starts-with(.,'System.')]", mgr))
	{
		var reference = element.Value;

		if(!exclude.Contains(reference))
			if (File.Exists(Path.Combine(designTime, reference + ".dll")))
			{
				var @private = element.OwnerElement.SelectSingleNode("a:Private[.='True']", mgr);
				@private.InnerText = "False";
				hasChanges = true;
				affected.Add(reference);
			}
	}

	if (hasChanges)
	{
		xmlDoc.Save(projectFile);
		projectFile.Dump();
	}
}

affected.Distinct().OrderBy(r=>r).ToList().Dump("Affected references:");
exclude.Dump("Excluded references:");