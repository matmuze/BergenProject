using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MyWindow : EditorWindow
{

	// Add menu item named "My Window" to the Window menu
	[MenuItem("CellUnity/Show Window")]
	public static void ShowWindow()
	{
		//Show existing window instance. If one doesn't exist, make one.
		EditorWindow.GetWindow(typeof(MyWindow));
	}
	
	void OnGUI()
	{
		if(GUILayout.Button ("Load MCell Scene")) 
		{
			//GameObject.Find("Main Script").GetComponent<MainScript>().LoadScene();

			LoadScene();
		}
	}

	string[] mol;

	public void LoadScene()
	{
		string text = System.IO.File.ReadAllText(@"mcell\Scene.molecules.mdl");

		string[] split = text.Split(new char[] {'{','}'}, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where<string>(s => !string.IsNullOrEmpty(s)).ToArray();

		List<string> res = new List<string> ();

		for(int i = 1; i < split.Length; i+=2 )
		{
			res.Add(split[i]);
		}
		
		mol = res.ToArray ();
	}
}