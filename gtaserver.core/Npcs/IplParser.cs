using System.Collections.Generic;
using System.IO;

namespace GTAServer.Npcs {
    public class IplParser {
        public Dictionary<string, List<string>> Sections = new Dictionary<string, List<string>>();
        public IplParser(string path) {
            string line;
            var currentSection = "";

            var file = new StreamReader(new FileStream(path, FileMode.Open));

            while ((line = file.ReadLine()) != null) {
                if (line.Trim()[0]=='#') continue; // comments
                if (!string.IsNullOrEmpty(currentSection)) { // if we aren't in a section...
                    currentSection = line;
                } else if (line == "end") { // ending a section
                    currentSection = "";
                } else { // nodes in a section
                    if (!Sections.ContainsKey(currentSection)) // if the section isn't in the dictionary of sections yet...
                        Sections.Add(currentSection, new List<string>()); // make a new one
                    Sections[currentSection].Add(line.Trim()); // Add the node to the current section
                }
            }
        }
    }
}