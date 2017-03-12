using System;
using System.Collections.Generic;
using System.IO;

namespace GTAServer.Npcs {
    public class IplSection {
        public List<string> SectionContents = new List<string>();
    }
    public class IplParser {
        public Dictionary<string, IplSection> Sections = new Dictionary<string, IplSection>();
        public IplParser(string path) {
            string line;
            var currentSection = "";

            var file = new StreamReader(new FileStream(path, FileMode.Open));

            while ((line = file.ReadLine()) != null) {
                if (line.Trim()[0] == '#')
                {
                    Console.WriteLine("type: comment - " + line);
                    continue; // comments
                }
                //if (!string.IsNullOrEmpty(currentSection) && !line.Contains(",")) { // if we aren't in a section...
                if (line.Contains(",")) {
                    Console.WriteLine("type: node -  " + line.Trim());
                    Sections[currentSection].SectionContents.Add(line.Trim()); // Add the node to the current section
                } else if (line == "end") { // ending a section
                    //Console.WriteLine("end of section: " + currentSection);
                    Console.WriteLine("type: sectionEnd - " + line);
                    currentSection = "";
                } else { // nodes in a section
                    Console.WriteLine("type: sectionStart - " + line);
                    //Console.WriteLine("new section: " + line);
                    currentSection = line;
                    Sections.Add(currentSection, new IplSection());
                }
            }
        }
    }
}