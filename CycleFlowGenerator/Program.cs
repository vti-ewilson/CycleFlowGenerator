// Usage: ./CycleFlowGenerator.exe Path/To/ClassesFolder nameOfOutputFile
// Run from vs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.SqlServer.Server;

namespace CycleFlowGenerator {

	class ManualCommands
	{
		string name;
		List<Step> startingSteps;
	}

	class Edge {
		public Edge(Step from, Step to, string fromSide, string toSide, string label, int id) {
			this.from = from;
			this.to = to;
			this.fromSide = fromSide;
			this.toSide = toSide;
			this.label = label;
			this.id = id;
		}

		public Step from;
		public Step to;
		public string fromSide;
		public string toSide;
		public string label;
		public int id;
	}

	class Step {
		public Step(string text, int height, int id, string color, bool parsed, bool visited, bool isParent, bool placed)
			: this(text, height, (15 * text.Length) + 85, id, color, parsed, visited, isParent, placed) {}

		public Step(string text, int height, int width, int id, string color, bool parsed, bool visited, bool isParent, bool placed) {
			this.text = text;
			this.width = (15 * text.Length) + 85;
			this.height = height;
			this.width = width;
			this.id = id;
			this.color = color;
			this.parsed = parsed;
			this.visited = visited;
			this.isParent = isParent;
			this.placed = placed;
		}

		public string text;
		public int id;
		public int x;
		public int y;
		public int width;
		public int height;
		public string color;
		public List<Edge> edges = new List<Edge>();
		public Step parent;
		public List<Step> leftChildren = new List<Step>();
		public List<Step> rightChildren = new List<Step>();
		public bool parsed;
		public bool visited;
		public bool isParent;
		public bool placed;
	}

	class CycleFlowGenerator {
		public SortedDictionary<string, Step> startingSteps = new SortedDictionary<string, Step>();
		public SortedDictionary<string, Step> allSteps = new SortedDictionary<string, Step>();
		public int totalEdges;
		StreamWriter writer;
		public int boxWidth = 300;
		public int boxHeight = 75;
		private int nextID = 0;
		private string[] lines;
		private string[] mcLines;
		private int lowestX = 0;
		private int lowestY = 0;
		private int offsetX = 250;
		private int offsetY = 150;
		string canvasPath;
		string saveFolder;
		Random colorGenerator = new Random();

		public CycleFlowGenerator(string classFolder, string flowFile) {

			try
			{
				List<string> savePath = File.ReadLines("../../SavePath.txt").ToList();
				if(savePath.Count <= 0) throw new Exception("");
				saveFolder = savePath[0].Trim('"', '\\');
			}
			catch (Exception ex)
			{ 
				string docFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
				saveFolder = docFolder + "\\Obsidian Vault\\Generated";
			}

			saveFolder += "\\" + flowFile;
			Directory.CreateDirectory(saveFolder);
			Directory.CreateDirectory(saveFolder + "\\Cycles");
			canvasPath = saveFolder + "\\" + flowFile + ".canvas";


			writer = new StreamWriter(canvasPath);
			lines = File.ReadAllLines(classFolder + "\\CycleSteps.cs");
			mcLines = File.ReadAllLines(classFolder + "\\ManualCommands.cs");

			ReadAllSteps();

			Step cycleFail = new Step("CycleFail", 300, 2000, getNextID(), "1", true, true, true, true);
			allSteps.Add(cycleFail.text, cycleFail);

			Step cyclePass = new Step("CyclePass", 300, 2000, getNextID(), "4", true, true, true, true);
			allSteps.Add(cyclePass.text, cyclePass);

			printAllSteps();
		}

		public void ReadManualCommands() {
			Regex cmdPattern = new Regex(@"public virtual void (\w+)\(\)");
			foreach(var line in mcLines)
			{

			}
		}

		public string GetRandomColor()
		{
			var color = String.Format("#{0:X6}", colorGenerator.Next(0x1000000));
			return color;
		}

		// Reads initial CycleStep declarations and creates Step objects
		public void ReadAllSteps() {
			string line;
			Regex rx = new Regex(@"(\w+)(,)");
			Regex rx2 = new Regex(@"(\w+)(;)");
			MatchCollection matches;
			bool inSteps = false;
			int fileInd;
			List<string> excludedSteps;

			excludedSteps = File.ReadAllLines("../../ExcludedSteps.txt").ToList();

			for(fileInd = 0; fileInd < lines.Length; fileInd++) { 
				line = lines[fileInd];
				Console.WriteLine(line);

				if(line == null) return;
				if(line.Contains("public CycleStep")) { 
					inSteps = true;
				}
				if(inSteps) {
					//Console.WriteLine(line);
					if(line == null) return;
					matches = rx.Matches(line);
					foreach(Match match in matches) {
						Console.WriteLine(match.Groups[1].Value);
						if(match.Groups[1].Value[0] == '/') break;
						if(!allSteps.ContainsKey(match.Groups[1].Value) && !excludedSteps.Contains(match.Groups[1].Value)) {
							Step step = new Step(match.Groups[1].Value, boxHeight, getNextID(), GetRandomColor(), false, false, true, false); ;

							allSteps.Add(step.text, step);
						}
							
					}
					matches = rx2.Matches(line);
					foreach(Match match in matches) {
						Console.WriteLine(match.Groups[1].Value);
						if(match.Groups[1].Value[0] == '/') break;
						if(!allSteps.ContainsKey(match.Groups[1].Value)) {
							Step step = new Step(match.Groups[1].Value, boxHeight, getNextID(), GetRandomColor(), false, false, true, false);
							allSteps.Add(step.text, step);
						}

					}
					if(line.Contains(';')) {
						break;
					}

				}

			}
		}

		// Searches _Passed or _Failed for next step in cycle
		private void FindNextStep(Step step, bool pass, int brackets, int fileInd) {
			int openBrackets = brackets;
			int closeBrackets = 0;
			bool inFunction = true;
			string line;
			Regex rx = new Regex(@"(\w+)(\.Start\(\);)");
			Regex rxPass = new Regex(@"(CyclePass\()([\s\S]+)?\)");
			Regex rxFail = new Regex(@"(CycleFail\()([\s\S]+)?\)");
			MatchCollection matches;

			while(inFunction) {
				if(fileInd < lines.Length) {
					fileInd++;
					line = lines[fileInd];
				} else break;
				//Console.WriteLine("line in function: " + line);
				for(int i = 0; i < line.Length; i++) {
					if(line[i] == '{') openBrackets++;
					if(line[i] == '}') closeBrackets++;
				}
				if(openBrackets == closeBrackets && openBrackets > 0) inFunction = false;

				// CyclePass found
				if(rxPass.IsMatch(line)) {
					Console.WriteLine("adding: CyclePass");
					if(pass) {
						step.rightChildren.Add(allSteps["CyclePass"]);
					} else {
						step.leftChildren.Add(allSteps["CyclePass"]);
					}

				}

				// CycleFail found
				if(rxFail.IsMatch(line)) {
					Console.WriteLine("adding: CycleFail");
					if(pass) {
						step.rightChildren.Add(allSteps["CycleFail"]);
					} else {
						step.leftChildren.Add(allSteps["CycleFail"]);
					}
				}

				// Step.Start() found
				matches = rx.Matches(line);
				foreach(Match match in matches) {
					//Console.WriteLine(match.Groups[1].Value);
					if(match.Groups[1].Value == "CycleComplete") {
						return;
					}
					if(match.Groups[1].Value[0] == '/') break;
					Console.WriteLine("adding: " + match.Groups[1].Value);

					// Add found step to list of children
					if(pass) {
						try {
							step.rightChildren.Add(allSteps[match.Groups[1].Value]);
						} catch(Exception e) {
							continue;
						}
					} else {
						try {
							step.leftChildren.Add(allSteps[match.Groups[1].Value]);
						} catch(Exception e) {
							continue;
						}
					}
					allSteps[match.Groups[1].Value].parent = step;
					allSteps[match.Groups[1].Value].isParent = false;

					// Parse found step
					Parse(allSteps[match.Groups[1].Value]);
				}
			}
		}

		// Searches file for a step's _Passed and _Failed function
		public void Parse(Step step) {
			if(step.parsed) return;
			step.parsed = true;
			string passFunction = step.text + "_Passed(";
			string failFunction = step.text + "_Failed(";
			int fileInd;
			string line;

			Console.WriteLine("looking for: " + passFunction);

			for(fileInd = 0; fileInd < lines.Length; fileInd++){
				line = lines[fileInd];
				//Console.WriteLine("line: " + line);

				if(line.Contains(passFunction)) {
					Console.WriteLine("line: " + line);
					if(line.Contains('{')) {
						FindNextStep(step, true, 1, fileInd);
					} else {
						FindNextStep(step, true, 0, fileInd);
					}
					break;
				}
			}

			Console.WriteLine("looking for: " + failFunction);

			for(fileInd = 0; fileInd < lines.Length; fileInd++) {
				line = lines[fileInd];

				if(line.Contains(failFunction)) {
					if(line.Contains('{')) {
						FindNextStep(step, false, 1, fileInd);
					} else {
						FindNextStep(step, false, 0, fileInd);
					}
					break;
				}
			}

		}

		private int PlaceNodes(Step step) {
			if(step.visited) return step.x;
			step.visited = true;
			for(int i = 0; i < step.leftChildren.Count; i++) {
				if(step.leftChildren[i].placed) {

				} else {
					step.leftChildren[i].x = step.x - offsetX + (offsetX * 2 * i);
					step.leftChildren[i].y = step.y + offsetY;
					step.leftChildren[i].placed = true;
					if(step.leftChildren[i].y > lowestY) {
						lowestY = step.leftChildren[i].y;
						lowestX = step.leftChildren[i].x;
					}
					PlaceNodes(step.leftChildren[i]);
				}
				Edge edge = new Edge(step, step.leftChildren[i], "left", "top", "Fail", getNextID());

				step.edges.Add(edge);
				totalEdges++;
			}

			for(int i = 0; i < step.rightChildren.Count; i++) {
				if(step.rightChildren[i].placed) {

				} else {
					step.rightChildren[i].x = step.x + offsetX + (offsetX * 2 * i);
					step.rightChildren[i].y = step.y + offsetY;
					step.rightChildren[i].placed = true;
					if(step.rightChildren[i].y > lowestY) {
						lowestY = step.rightChildren[i].y;
						lowestX = step.rightChildren[i].x;
					}
					PlaceNodes(step.rightChildren[i]);

				}
				Edge edge = new Edge(step, step.rightChildren[i], "right", "top", "Pass", getNextID());

				step.edges.Add(edge);
				totalEdges++;

			}

			return step.x;
		}

		// Iterate through steps and write to canvas file
		private void WriteToCanvas() {
			writer.Write("{\n\t\"nodes\":[\n");
			foreach(var step in allSteps) {
				writer.Write("\t\t{\"type\":\"text\",\"text\":\"" + step.Value.text);
				writer.Write("\",\"id\":\"" + step.Value.id.ToString("X16"));
				writer.Write("\",\"x\":" + step.Value.x.ToString());
				writer.Write(",\"y\":" + step.Value.y.ToString());
				writer.Write(",\"width\":" + step.Value.width.ToString());
				writer.Write(",\"height\":" + step.Value.height.ToString());
				writer.Write(",\"color\":\"" + step.Value.color.ToString() + "\"}");
				if(step.Value != allSteps.Values.Last()) {
					writer.Write(",");
				}
				writer.Write("\n");
			}
			writer.Write("\t],\n\t\"edges\":[\n");

			int edges = 0;
			int numEdges = 0;
			foreach(var step in allSteps) numEdges += step.Value.edges.Count;
			foreach(var step in allSteps) {
				for(int j = 0; j < step.Value.edges.Count; j++) {
					Edge edge = step.Value.edges[j];

					writer.Write("\t\t{\"id\":\"" + edge.id.ToString("X16"));
					writer.Write("\",\"fromNode\":\"" + edge.from.id.ToString("X16"));
					writer.Write("\",\"fromSide\":\"" + edge.fromSide);
					writer.Write("\",\"toNode\":\"" + edge.to.id.ToString("X16"));
					writer.Write("\",\"toSide\":\"" + edge.toSide);
					writer.Write("\",\"label\":\"" + edge.label + "\"}");
					edges++;
					if(edges != numEdges)
					{
						writer.Write(",");
					}
					writer.Write("\n");
				}
			}
			writer.Write("\t]\n}");

			writer.Close();
		}

		public string Generate() {
			int parentX = 0;

			ReadAllSteps();

			foreach(var step in allSteps) {
				Parse(step.Value);
			}

			List<string> toRemove = new List<string>();
			foreach(var step in allSteps) {
				if(step.Value.leftChildren.Count == 0 && step.Value.rightChildren.Count == 0)
				{ // Hide steps without children, cant remove in foreach
					toRemove.Add(step.Key);
				}
				else
				{ // Add parentless steps to startingSteps
					if(step.Value.isParent) startingSteps.Add(step.Key, step.Value);
				}
			}

			// remove here instead
			foreach(string step in toRemove)
			{
				if(step != "CyclePass" && step != "CycleFail") allSteps.Remove(step);
			}

			printAllSteps();

			foreach(var step in startingSteps) {
				step.Value.x = parentX;
				step.Value.y = 0;
				parentX = PlaceNodes(step.Value) + 1500;
			}

			allSteps["CyclePass"].x = lowestX + 1100;
			allSteps["CyclePass"].y = lowestY + 500;

			allSteps["CycleFail"].x = lowestX - 1100;
			allSteps["CycleFail"].y = lowestY + 500;

			
			WriteToCanvas();

			return canvasPath;
		}

		public void printAllSteps() {
			foreach(var step in allSteps) {
				Console.WriteLine(step.Value.text);
				for(int i = 0; i < step.Value.leftChildren.Count; i++) {
					Console.WriteLine("\t" + step.Value.leftChildren[i].text);
				}
				for(int i = 0; i < step.Value.rightChildren.Count; i++) {
					Console.WriteLine("\t" + step.Value.rightChildren[i].text);
				}
			}
		}

		public int getNextID() {
			nextID++;
			return nextID;
		}

	}



	internal class Program {

		static void Main(string[] args) {
			string classFolder, flowFile;

			Console.Write("Enter path to Classes folder: ");
			classFolder = Console.ReadLine().Trim('"');

			Console.Write("Enter name of Obsidian canvas file: ");
			flowFile = Console.ReadLine();

			CycleFlowGenerator generator = new CycleFlowGenerator(classFolder, flowFile);


			string path = generator.Generate();

			Console.WriteLine(generator.totalEdges.ToString());

			Console.WriteLine("Flow chart created at: " + path + "\n\npress enter to close.");
			Console.ReadLine();
		}
	}
}
