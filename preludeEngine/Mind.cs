/*
 * Created by SharpDevelop.
 * User: novalis78
 * Date: 17.11.2004
 * Time: 20:12
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 * 
 */

 //TODO: more keywords, random start when too long idle
 //TODO: update from server, algorithm visualizer
 	
using System;
using System.Collections;
using System.Windows.Forms;
using System.Collections.Specialized;
using System.Text;
using NLog;
using System.Web;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace PreludeEngine
{
	/// <summary>
	/// Description of Class1.
	/// </summary>
	/// 
	public class Mind : Brain
	{
		public Mind(string fileName, bool fullPath) : base(fileName,fullPath){}

		private static string input 	 = "";
		private static string lastOutput = "";
		private static StringCollection bestMatchesList = new StringCollection();
		protected Hashtable botsMemory			   	= new Hashtable();
        protected ArrayList semanticRecognition = new ArrayList();
		private static Hashtable matchedMemoryValues   	= new Hashtable();
		private const  int MAX_NUMBER_OF_IDENT_ENTRIES 	= 5;
		private const  int MAX_MATCHES_ALLOWED    		= 5;
        public enum MatchingAlgorithm { Basic, Jaccard, Levensthein, Dice}
		public  int memorySize = 0;
		public  bool proactiveMode = false;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private GSWebClient client = new GSWebClient();
		
		#region memory loading operations
		public void analyzeShortTermMemory()
		{
			purifyBotsMind();			
			StringCollection sc = new StringCollection();
			botsMemory.Clear();
			if(fullPathName == "")
				sc = readBrainFile();
			else
				sc = readBrainFile(fullPathName);
			StringEnumerator ii = sc.GetEnumerator();
			while(ii.MoveNext())
			{
				if(!botsMemory.Contains(parseForThoughts(ii.Current)))
					botsMemory.Add(parseForThoughts(ii.Current), parseForWords(ii.Current));

                //this can be turned off. It is needed to improve the Jaccard Distance Algorithm in
                //calculating a similarity between input and memory based on semantic proximity


			}
			writeToLogFile("Number of memo entries", botsMemory.Count.ToString());
			memorySize = botsMemory.Count;
			return;
		}
		
		private string parseForWords(string a)
		{
			string b = "";
			string tag = "USER";
			b = parseXMLContent(a, tag);
			return b;
		}
		
		private string parseForThoughts(string a)
		{
			string b = "";
			string tag = "BOT";
			b = parseXMLContent(a, tag);
			return b;
		}
		
		private string parseXMLContent(string xmlString, string xmlTag)
		{
			string content = "";
			string startTag = "<" + xmlTag + ">";
			string endTag   = "</" + xmlTag + ">";
			int startPos = xmlString.IndexOf(startTag);
			int endPos   = xmlString.IndexOf(endTag);
			if(startPos != -1 && endPos != -1)
			{
				startPos = startPos + startTag.Length;
				content = xmlString.Substring(startPos, (endPos - startPos));
			}
			return content;
		}
		#endregion
		
		#region memory saving operations
		private void addNewInputToCurrentMemory(string input)
		{
			//user's input becomes possible output!
			//bot's last output becomes possible input!
			if(lastOutput != "")
			{
				if(!botsMemory.Contains(input))
					botsMemory.Add(input, lastOutput);
				writeToLogFile("", "-----------------------");
				writeToLogFile("LAST OUTPUT", lastOutput);				
				writeToLogFile("NEW INPUT", input);
			}
		}
		
		public void prepareCurrentMemoryForDisc()
		{
			StringCollection a = joinWordsAndThoughts();
			writeBrainFile(a);
		}
		public void prepareCurrentMemoryForDisc(string saveAs)
		{
			StringCollection a = joinWordsAndThoughts();
			writeBrainFile(a, saveAs);
		}		
		
		private StringCollection joinWordsAndThoughts()
		{
			StringCollection sc = new StringCollection();
			IDictionaryEnumerator de = botsMemory.GetEnumerator();
			while(de.MoveNext())
			{
				sc.Add("<USER>" + de.Value.ToString() + "</USER>" + "<BOT>" + de.Key.ToString() + "</BOT>");
			}
			return sc;
		}
		#endregion
		
		//gets input returns output
		public string listenToInput(string receivedInput)
		{
			input = receivedInput;	
			string output = "";
			addNewInputToCurrentMemory(input);
			checkInputForHiddenCommands(input);
			output = thinkItOver(input);
			lastOutput = output;
			return output;
		}
		
		#region thinking process
		//starts thinking process
		private string thinkItOver(string a)
		{
			string b = "";
            loadAuxilliaryKnowledgeIntoMemory(a);
			matchInputWithMemory(a);
			findBestMatchWithinMemory();
	
            //testing real quantum state induced random fluctuation - cool!
            //Dr Penrose would be happy
            if(quantumRandomness)
                b = randomQuantumSelectAnswer(bestMatchesList);
            else
			    b = randomSelectAnswer(bestMatchesList);
			
            //dont allow bot to repeate its last sentence
			if(b == lastOutput) b = a;
			//bot echoes if it has no proper answer
			if(bestMatchesList.Count <= 0) b = a;
			return b;
		}

        private void loadAuxilliaryKnowledgeIntoMemory(string i)
        {
            if (botsMemory != null)
            { 
                List<string> externalAnswers = getExternalAnswers(i);
                foreach (string a in externalAnswers)
                {
                    botsMemory.Add(i, a);
                }
            }
        }

        private List<string> getExternalAnswers(string a)
        {
            List<string> externalAnswers = new List<string>();
            //load all plugin dll's
            //through input sentence at them
            //collect their responses
            return externalAnswers;
        }


		//returns position of best match for input in memory
		private void matchInputWithMemory(string a)
		{
			double matchRate = 0;
			matchedMemoryValues.Clear();
            
            //experimental - google query addon
            //string foundKnowledge = ParseForKnowledge(a);
            //if (!String.IsNullOrEmpty(foundKnowledge))
            //    botsMemory.Add(a, foundKnowledge);

			ArrayList inputSentenceTokenized = tokenizeString(a);
			IDictionaryEnumerator de = botsMemory.GetEnumerator();
			//run through memory
			while(de.MoveNext())
			{
				ArrayList t = tokenizeString((string)de.Value);
                
                if(associater == MatchingAlgorithm.Levensthein)
				    matchRate   = calculateMatchRateLS(inputSentenceTokenized, t);
                else if (associater == MatchingAlgorithm.Dice)
                    matchRate = calculateMatchRateDice(inputSentenceTokenized, t);
                else if (associater == MatchingAlgorithm.Jaccard)
                    matchRate = calculateMatchRateJ(inputSentenceTokenized, t);
                else
                    matchRate = calculateMatchRate(inputSentenceTokenized, t);

                    if(!matchedMemoryValues.Contains(de.Key))
				        if(matchRate != 0) 
                            matchedMemoryValues.Add(de.Key, matchRate);
			}
			return;
		}

        private string ParseForKnowledge(string a)
        {
            string page = client.DownloadString("http://www.google.com/search?hl=en&site=&source=hp&q=%22" + HttpUtility.UrlEncode(a) + "%22&oq=%22how+are+you+today%22");
            //search for a.toLower() inside <em><em>
            page = page.ToLower();
            string searchItem = "<b>"+a.ToLower()+"</b>";
            string result = "";
            while(page.Contains(searchItem))
            {
                int pos = page.IndexOf(searchItem);
                int posE = page.IndexOfAny( new char[] {'?', '.'}, pos+searchItem.Length+1, 200);
                if(posE > pos)
                {
                    result = page.Substring(pos+searchItem.Length, posE - (pos+searchItem.Length));
                    page = page.Remove(0, posE + 1);
                }
                else
                    page = page.Remove(0, posE + 1);
            }

            return result;
            //MatchCollection matches = Regex.Matches(npage, "\\[\\\"\\d{5,10}\\\"\\]");//"itemID" : "123731"
            //foreach (Match m in matches)
            //{
            //    string bib = Helper.ExtractNumbers(m.Value);
            //    if (!records.Contains(bib))
            //        records.Add(bib);
            //}
            
        }

		
		private void findBestMatchWithinMemory()
		{
			double i = 0;
			double highestValue = 0;
			bestMatchesList.Clear();
			if(matchedMemoryValues.Count > 0)
			{		
				IDictionaryEnumerator de = matchedMemoryValues.GetEnumerator();
				while(de.MoveNext())
				{
					if(highestValue <= (double)de.Value)
					{
						highestValue = (double)de.Value;
					}
				}
				//jetzt kennen wir den hchsten Wert,
				//zeit, die entsprechend hchstenwertigen stze rauszufischen:
				for(i = highestValue; i > 0; i--)
				{
                    logger.Trace("Finding best match within memory: " + i); 
					IDictionaryEnumerator re = matchedMemoryValues.GetEnumerator();
					while(re.MoveNext())
					{
                        if (i == (double)re.Value)
                        {
                            bestMatchesList.Add((string)re.Key);
                            logger.Trace("Added to best list: [" + (double)re.Value + "]\t->" + (string)re.Key);
                        }
						if(bestMatchesList.Count > MAX_MATCHES_ALLOWED)
							break;						
					}					
				}	
			}
		}
		
		//select randomly one sentence from preselected list of 
		//best responses
		private string randomSelectAnswer(StringCollection a)
		{
			string b = "";
			int z    = 0; 
			if(a.Count <= 0)
				return b;
			else
			{
				Random r = new Random();
				z = r.Next(0, a.Count);
				b = a[z];
				return b;
			}
		}

        private string randomQuantumSelectAnswer(StringCollection a)
        {
            try
            {
                QRNG pqDLL = new QRNG();
                Int32 iRet = 0;
                if (pqDLL.CheckDLL() == true)
                {
                    StringBuilder strUser = new StringBuilder(32);
                    StringBuilder strPass = new StringBuilder(32);

                    strUser.Insert(0, "llopin");
                    strPass.Insert(0, "kR63LyowFi8n");
                    try
                    {
                        iRet = QRNG.qrng_connect(strUser, strPass);
                    }
                    catch (System.Exception ex)
                    {
                        logger.Trace("Could not connect to Quantum Generator");
                        randomSelectAnswer(a);
                    }
                    if (iRet == 0)
                    {
                        Int32 iCreatedNumbers, iNumberOfValues;
                        Double[] fArray;
                        iNumberOfValues = 1;
                        fArray = new Double[iNumberOfValues];
                        iCreatedNumbers = 0;
                        try
                        {
                            iRet = QRNG.qrng_get_double_array(ref fArray[0], iNumberOfValues, ref iCreatedNumbers);
                            if (iRet == 0)
                            {
                                double qrandom = fArray[0];

                                string b = "";
                                if (a.Count <= 0)
                                    return b;
                                else
                                {
                                    if (qrandom < 1)
                                    {
                                        int position = Convert.ToInt32(a.Count * qrandom);
                                        b = a[position];
                                        return b;
                                    }
                                    else
                                    {
                                        Console.WriteLine("oops: " + qrandom);
                                        return randomSelectAnswer(a);
                                    }
                                }
                            }
                            else
                                return randomSelectAnswer(a);
                        }
                        catch (System.Exception ex)
                        {
                            logger.Trace("Error trying to retrieve quantum fluctuation: " + ex.Message);
                            return randomSelectAnswer(a);
                        }
                    }
                    else
                        return randomSelectAnswer(a);
                }
                else
                {
                    return randomSelectAnswer(a);
                }
            }
            catch (System.Exception ex)
            {
                return randomSelectAnswer(a);
            }
        }
			
		#endregion
		
		
		#region mind purifying methods
		private void purifyBotsMind()
		{
			//killStickingPhrasesFromMemory();
			return;
		}
		//if bot sticks to a phrase simply limit its influence 
		//below a threshold number of occurences
		private void killStickingPhrasesFromMemory()
		{
			/*for(int r = 0; r < 2; r++)
			{
				int i = 0;
				string s = "";
				StringEnumerator se = words.GetEnumerator();
				while(se.MoveNext())
				{
					s = (string) se.Current;
					foreach(string o in words)
					{
						if( s == o) i++;
					}
					if(i > MAX_NUMBER_OF_IDENT_ENTRIES)
					{		//can delete only one word, count changes because of 
							//deletion
							writeToLogFile("deleting entry", s);
							writeToLogFile("because found it too often: ", i.ToString());
							int z = words.IndexOf(s);
							words.RemoveAt(z); 
							thoughts.RemoveAt(z);
							break;
					}
				}
			}*/
		}
		
		
		#endregion
		
		/*
		public static int MemorySize {
			get {
				return memorySize;
			}
			set {
				memorySize = value;
			}
		}
		*/
		#region utility methods
		private void checkInputForHiddenCommands(string a)
		{
			if((a.ToLower()).IndexOf("google") != -1)
			{
				string b = a.Substring(a.IndexOf("google") + 6);
				System.Diagnostics.Process.Start("IExplore", b);
			}
			if((a.ToLower()).IndexOf("open") != -1)
			{
				if(a.ToLower().IndexOf("word") != -1)
				{
					System.Diagnostics.Process.Start("winword");
				}
				if(a.IndexOf("notepad") != -1)
				{
					System.Diagnostics.Process.Start("notepad");
				}   
			}
			if((a.ToLower()).IndexOf("what") != -1)
			{
				if(a.ToLower().IndexOf("time") != -1)
				{
					System.Diagnostics.Process.Start("timedate.cpl");		
				}
			}
			if(a.ToLower().IndexOf("network") != -1)
			{
				System.Diagnostics.Process.Start("ncpa.cpl");			
			}
		}
		
		public void contributeClientMind()
		{
			PreLudeClient pc = new PreLudeClient();
			pc.connectToPreludeServer(botsMemory);
		}
		#endregion


        public bool quantumRandomness { get; set; }
        public MatchingAlgorithm associater { get; set; }
    }
}
