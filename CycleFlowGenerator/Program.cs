// Usage: ./CycleFlowGenerator.exe Path/To/ClassesFolder nameOfOutputFile
// Run from vs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CycleFlowGenerator
{

	class ManualCommand
	{
		public string name;
		public SortedDictionary<string, Step> startingSteps;
		public List<Edge> edges = new List<Edge>();
	}

	class Edge
	{
		public Edge(Step fromStep, Step toStep, string fromSide, string toSide, string label, int id)
		{
			this.from = fromStep.id.ToString("X16");
			this.to = toStep.id.ToString("X16");
			this.fromSide = fromSide;
			this.toSide = toSide;
			this.label = label;
			this.id = id;
		}

		public Edge(string from, string to, string fromSide, string toSide, string label, int id)
		{
			this.from = from;
			this.to = to;
			this.fromSide = fromSide;
			this.toSide = toSide;
			this.label = label;
			this.id = id;
		}

		public string from;
		public string to;
		public string fromSide;
		public string toSide;
		public string label;
		public int id;
	}

	class Step
	{
		public Step(string text, int height, int id, string color, bool parsed, bool visited, bool isParent, bool placed)
			: this(text, height, (15 * text.Length) + 85, id, color, parsed, visited, isParent, placed) { }

		public Step(string text, int height, int width, int id, string color, bool parsed, bool visited, bool isParent, bool placed)
		{
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

	class CycleFlowGenerator
	{
		public SortedDictionary<string, Step> startingSteps = new SortedDictionary<string, Step>();
		public SortedDictionary<string, Step> allSteps = new SortedDictionary<string, Step>();
		public List<ManualCommand> manualCommands = new List<ManualCommand>();
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
		string cycleFolder;
		Random colorGenerator = new Random();

		public CycleFlowGenerator(string classFolder, string flowFile)
		{

			try
			{
				List<string> savePath = File.ReadLines("../../SavePath.txt").ToList();
				if(savePath.Count <= 0) throw new Exception("");
				saveFolder = savePath[0].Trim('"', '\\');
			}
			catch(Exception ex)
			{
				string docFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
				saveFolder = docFolder + "\\Obsidian Vault\\Generated";
			}

			saveFolder += "\\" + flowFile;
			cycleFolder = saveFolder + "\\Cycles";
			Directory.CreateDirectory(saveFolder);
			Directory.CreateDirectory(cycleFolder);
			canvasPath = saveFolder + "\\" + flowFile + ".canvas";


			lines = File.ReadAllLines(classFolder + "\\CycleSteps.cs");
			mcLines = File.ReadAllLines(classFolder + "\\ManualCommands.cs");

			ReadAllSteps();

			Step cycleStart = new Step("CycleStart", boxHeight, getNextID(), "5", false, false, true, false);
			allSteps.Add(cycleStart.text, cycleStart);

			Step cycleFail = new Step("CycleFail", 300, 2000, getNextID(), "1", true, true, true, true);
			allSteps.Add(cycleFail.text, cycleFail);

			Step cyclePass = new Step("CyclePass", 300, 2000, getNextID(), "4", true, true, true, true);
			allSteps.Add(cyclePass.text, cyclePass);

			printAllSteps();

			ReadManualCommands();
		}

		public void ReadManualCommands()
		{
			bool inFunction = false;
			int openBrackets;
			int closeBrackets;
			Regex cmdPattern = new Regex(@"public \w* *void (\w+)\(\)");
			Regex rxStart = new Regex(@"(\w+)(\.Start\(\);)");
			ManualCommand command;
			SortedDictionary<string, Step> tempStartingSteps;

			for(int fileInd = 0; fileInd < mcLines.Length; fileInd++)
			{
				string line = mcLines[fileInd];
				if(line.Contains("ZeroTransducer"))
				{

				}
				openBrackets = 0;
				closeBrackets = 0;

				var cmdMatch = cmdPattern.Match(line);
				if(cmdMatch.Success)
				{
					if(cmdMatch.Groups[1].Value == "Start")
					{

					}
					inFunction = true;
					openBrackets = line.Count(c => c == '{');
					tempStartingSteps = new SortedDictionary<string, Step>();
					while(inFunction) // Iterate through lines in Manual command function
					{
						if(fileInd < mcLines.Length)
						{
							fileInd++;
							line = mcLines[fileInd];
						}
						else break;

						for(int i = 0; i < line.Length; i++)
						{
							if(line[i] == '{') openBrackets++;
							if(line[i] == '}') closeBrackets++;
						}
						if(openBrackets == closeBrackets && openBrackets > 0) inFunction = false;

						var matches = rxStart.Matches(line);
						foreach(Match match in matches)
						{
							string stepName = match.Groups[1].Value;
							if(allSteps.ContainsKey(stepName))
							{
								if(!tempStartingSteps.ContainsKey(stepName)) tempStartingSteps.Add(stepName, allSteps[stepName]);
							}
						}
					}
					if(tempStartingSteps.Count > 0) // Create ManualCommand if it starts any CycleSteps
					{
						command = new ManualCommand();
						command.name = cmdMatch.Groups[1].Value;
						command.startingSteps = tempStartingSteps;
						command.edges = new List<Edge>();
						foreach(var step in tempStartingSteps)
						{
							//command.edges.Add(new Edge())
						}
						manualCommands.Add(command);
					}
				}
			}
		}

		public string GetRandomColor()
		{
			var color = String.Format("#{0:X6}", colorGenerator.Next(0x1000000));
			return color;
		}

		// Reads initial CycleStep declarations and creates Step objects
		public void ReadAllSteps()
		{
			string line;
			Regex rx = new Regex(@"(\w+)(,)");
			Regex rx2 = new Regex(@"(\w+)(;)");
			MatchCollection matches;
			bool inSteps = false;
			int fileInd;
			List<string> excludedSteps;

			excludedSteps = File.ReadAllLines("../../ExcludedSteps.txt").ToList();

			for(fileInd = 0; fileInd < lines.Length; fileInd++)
			{
				line = lines[fileInd];
				Console.WriteLine(line);

				if(line == null) return;
				if(line.Contains("public CycleStep"))
				{
					inSteps = true;
				}
				if(inSteps)
				{
					//Console.WriteLine(line);
					if(line == null) return;
					matches = rx.Matches(line);
					foreach(Match match in matches)
					{
						Console.WriteLine(match.Groups[1].Value);
						if(match.Groups[1].Value[0] == '/') break;
						if(!allSteps.ContainsKey(match.Groups[1].Value) && !excludedSteps.Contains(match.Groups[1].Value))
						{
							Step step = new Step(match.Groups[1].Value, boxHeight, getNextID(), GetRandomColor(), false, false, true, false);

							allSteps.Add(step.text, step);
						}

					}
					matches = rx2.Matches(line);
					foreach(Match match in matches)
					{
						Console.WriteLine(match.Groups[1].Value);
						if(match.Groups[1].Value[0] == '/') break;
						if(!allSteps.ContainsKey(match.Groups[1].Value))
						{
							Step step = new Step(match.Groups[1].Value, boxHeight, getNextID(), GetRandomColor(), false, false, true, false);
							allSteps.Add(step.text, step);
						}

					}
					if(line.Contains(';'))
					{
						break;
					}

				}

			}
		}

		// Searches _Passed or _Failed for next step in cycle
		private void FindNextStep(Step step, bool pass, int brackets, int fileInd)
		{
			int openBrackets = brackets;
			int closeBrackets = 0;
			bool inFunction = true;
			string line;
			Regex rxStart = new Regex(@"(\w+)(\.Start\(\);)");
			Regex rxCycleStart = new Regex(@"(CycleStart\()([\s\S]+)?\)");
			Regex rxCyclePass = new Regex(@"(CyclePass\()([\s\S]+)?\)");
			Regex rxCycleFail = new Regex(@"(CycleFail\()([\s\S]+)?\)");
			MatchCollection matches;

			while(inFunction)
			{
				if(fileInd < lines.Length)
				{
					fileInd++;
					line = lines[fileInd];
				}
				else break;
				//Console.WriteLine("line in function: " + line);
				for(int i = 0; i < line.Length; i++)
				{
					if(line[i] == '{') openBrackets++;
					if(line[i] == '}') closeBrackets++;
				}
				if(openBrackets == closeBrackets && openBrackets > 0) inFunction = false;

				// CycleStart found
				if(rxCycleStart.IsMatch(line))
				{
					Console.WriteLine("adding: CycleStart");
					if(pass)
					{
						step.rightChildren.Add(allSteps["CycleStart"]);
					}
					else
					{
						step.leftChildren.Add(allSteps["CycleStart"]);
					}
				}

				// CyclePass found
				if(rxCyclePass.IsMatch(line))
				{
					Console.WriteLine("adding: CyclePass");
					if(pass)
					{
						step.rightChildren.Add(allSteps["CyclePass"]);
					}
					else
					{
						step.leftChildren.Add(allSteps["CyclePass"]);
					}
				}

				// CycleFail found
				if(rxCycleFail.IsMatch(line))
				{
					Console.WriteLine("adding: CycleFail");
					if(pass)
					{
						step.rightChildren.Add(allSteps["CycleFail"]);
					}
					else
					{
						step.leftChildren.Add(allSteps["CycleFail"]);
					}
				}

				// Step.Start() found
				matches = rxStart.Matches(line);
				foreach(Match match in matches)
				{
					//Console.WriteLine(match.Groups[1].Value);
					if(match.Groups[1].Value == "CycleComplete")
					{
						return;
					}
					if(match.Groups[1].Value[0] == '/') break;
					Console.WriteLine("adding: " + match.Groups[1].Value);

					// Add found step to list of children
					if(pass)
					{
						try
						{
							step.rightChildren.Add(allSteps[match.Groups[1].Value]);
						}
						catch(Exception e)
						{
							continue;
						}
					}
					else
					{
						try
						{
							step.leftChildren.Add(allSteps[match.Groups[1].Value]);
						}
						catch(Exception e)
						{
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

		public void ParseCycleStart(Step step)
		{
			if(step.parsed) return;
			step.parsed = true;
			int fileInd;
			string line;
			Regex rxCycleStart = new Regex(@"(CycleStart\()([\s\S]+)?\)");

			Console.WriteLine("looking for: CycleStart");

			for(fileInd = 0; fileInd < lines.Length; fileInd++)
			{
				line = lines[fileInd];

				if(rxCycleStart.IsMatch(line))
				{
					Console.WriteLine("line: " + line);
					if(line.Contains('{'))
					{
						FindNextStep(step, true, 1, fileInd);
					}
					else
					{
						FindNextStep(step, true, 0, fileInd);
					}
					break;
				}
			}
		}

		// Searches file for a step's _Passed and _Failed function
		public void Parse(Step step)
		{
			if(step.parsed) return;
			step.parsed = true;
			string passFunction = step.text + "_Passed(";
			string failFunction = step.text + "_Failed(";
			int fileInd;
			string line;

			Console.WriteLine("looking for: " + passFunction);

			for(fileInd = 0; fileInd < lines.Length; fileInd++)
			{
				line = lines[fileInd];

				Regex passReg = new Regex(@"\s" + Regex.Escape(passFunction));
				if(passReg.IsMatch(line))
				{
					Console.WriteLine("line: " + line);
					if(line.Contains('{'))
					{
						FindNextStep(step, true, 1, fileInd);
					}
					else
					{
						FindNextStep(step, true, 0, fileInd);
					}
					break;
				}
			}

			Console.WriteLine("looking for: " + failFunction);

			for(fileInd = 0; fileInd < lines.Length; fileInd++)
			{
				line = lines[fileInd];

				Regex failReg = new Regex(@"\s" + Regex.Escape(failFunction));
				if(failReg.IsMatch(line))
				{
					if(line.Contains('{'))
					{
						FindNextStep(step, false, 1, fileInd);
					}
					else
					{
						FindNextStep(step, false, 0, fileInd);
					}
					break;
				}
			}

		}

		private int PlaceNodes(Step step)
		{
			if(step.visited) return step.x;
			step.visited = true;
			for(int i = 0; i < step.leftChildren.Count; i++)
			{
				if(!step.leftChildren[i].placed)
				{ 
					step.leftChildren[i].x = step.x - offsetX + (offsetX * 2 * i);
					step.leftChildren[i].y = step.y + offsetY;
					step.leftChildren[i].placed = true;
					if(step.leftChildren[i].y > lowestY)
					{
						lowestY = step.leftChildren[i].y;
						lowestX = step.leftChildren[i].x;
					}
					PlaceNodes(step.leftChildren[i]);
				}
				Edge edge = new Edge(step, step.leftChildren[i], "left", "top", "Fail", getNextID());

				step.edges.Add(edge);
				totalEdges++;
			}

			for(int i = 0; i < step.rightChildren.Count; i++)
			{
				if(!step.rightChildren[i].placed)
				{
					step.rightChildren[i].x = step.x + offsetX + (offsetX * 2 * i);
					step.rightChildren[i].y = step.y + offsetY;
					step.rightChildren[i].placed = true;
					if(step.rightChildren[i].y > lowestY)
					{
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
		private void WriteToCanvas(string entryPoint)
		{
			writer.Write("{\n\t\"nodes\":[\n");

			if(entryPoint != null)
			{
				writer.Write("\t\t{\"type\":\"text\",\"text\":\"" + entryPoint);
				writer.Write("\",\"id\":\"" + getNextID().ToString("X16"));
				writer.Write("\",\"x\":0");
				writer.Write(",\"y\":-200");
				writer.Write(",\"width\":" + ((15 * entryPoint.Length) + 85).ToString());
				writer.Write(",\"height\":" + boxHeight.ToString());
				writer.Write(",\"color\":\"3\"}");
				writer.Write(",");
				writer.Write("\n");
			}

			var placedSteps = allSteps.Where(s => s.Value.placed).ToList();
			//foreach(var step in placedSteps) {
			for(int i = 0; i < placedSteps.Count; i++)
			{
				var step = placedSteps[i];
				if(step.Value.placed)
				{
					writer.Write("\t\t{\"type\":\"text\",\"text\":\"" + step.Value.text);
					writer.Write("\",\"id\":\"" + step.Value.id.ToString("X16"));
					writer.Write("\",\"x\":" + step.Value.x.ToString());
					writer.Write(",\"y\":" + step.Value.y.ToString());
					writer.Write(",\"width\":" + step.Value.width.ToString());
					writer.Write(",\"height\":" + step.Value.height.ToString());
					writer.Write(",\"color\":\"" + step.Value.color.ToString() + "\"}");
					if(i != placedSteps.Count - 1)
					{
						writer.Write(",");
					}
					writer.Write("\n");
				}
			}
			writer.Write("\t],\n\t\"edges\":[\n");

			int edges = 0;
			int numEdges = 0;
			foreach(var step in placedSteps)
			{
				numEdges += step.Value.edges.Count;
			}
			foreach(var step in placedSteps)
			{
				if(step.Value.placed)
				{
					for(int j = 0; j < step.Value.edges.Count; j++)
					{
						Edge edge = step.Value.edges[j];

						writer.Write("\t\t{\"id\":\"" + edge.id.ToString("X16"));
						writer.Write("\",\"fromNode\":\"" + edge.from);
						writer.Write("\",\"fromSide\":\"" + edge.fromSide);
						writer.Write("\",\"toNode\":\"" + edge.to);
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
			}
			writer.Write("\t]\n}");

			writer.Close();
		}

		private void ResetNodePositions()
		{
			foreach(var item in allSteps)
			{
				item.Value.placed = false;
				item.Value.visited = false;
			}
		}

		public string Generate()
		{
			int parentX = 0;

			ReadAllSteps();

			foreach(var step in allSteps)
			{
				if(step.Key == "CycleStart")
					ParseCycleStart(step.Value);
				else
					Parse(step.Value);
			}

			List<string> toRemove = new List<string>();
			foreach(var step in allSteps)
			{
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
				//if(step != "CyclePass" && step != "CycleFail") allSteps.Remove(step);
			}

			printAllSteps();

			foreach(var step in startingSteps)
			{
				step.Value.x = parentX;
				step.Value.y = 0;
				parentX = PlaceNodes(step.Value) + 1500;
			}

			allSteps["CyclePass"].x = lowestX + 1100;
			allSteps["CyclePass"].y = lowestY + 500;

			allSteps["CycleFail"].x = lowestX - 1100;
			allSteps["CycleFail"].y = lowestY + 500;

			writer = new StreamWriter(canvasPath);
			WriteToCanvas(null);

			foreach(var cmd in manualCommands)
			{
				ResetNodePositions();
				writer = new StreamWriter(cycleFolder + "\\" + cmd.name + ".canvas");

				parentX = 0;
				foreach(var step in cmd.startingSteps)
				{
					step.Value.x = parentX;
					step.Value.y = 0;
					step.Value.placed = true;
					parentX = PlaceNodes(step.Value) + 1500;
				}

				WriteToCanvas(cmd.name);
			}

			return canvasPath;
		}

		public void printAllSteps()
		{
			foreach(var step in allSteps)
			{
				Console.WriteLine(step.Value.text);
				for(int i = 0; i < step.Value.leftChildren.Count; i++)
				{
					Console.WriteLine("\t" + step.Value.leftChildren[i].text);
				}
				for(int i = 0; i < step.Value.rightChildren.Count; i++)
				{
					Console.WriteLine("\t" + step.Value.rightChildren[i].text);
				}
			}
		}

		public int getNextID()
		{
			nextID++;
			return nextID;
		}

	}



	internal class Program
	{

		static void Main(string[] args)
		{
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
