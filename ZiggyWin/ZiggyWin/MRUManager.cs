using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;

public class MRUManager
{
	#region Private members
	private string NameOfProgram;
	private string SubKeyName;
	private ToolStripMenuItem ParentMenuItem;
	private Action<object, EventArgs> OnRecentFileClick;
	private Action<object, EventArgs> OnClearRecentFilesClick;
    private List<string> fullPath = new List<string>();
    private const int MAX_FILE_PATH_CHARS = 40;

    [DllImport("shlwapi.dll")]
    static extern bool PathCompactPathEx([Out] StringBuilder pszOut, string szPath, int cchMax, int dwFlags);

    static string TruncatePath(string path, int length)
    {
        StringBuilder sb = new StringBuilder();
        PathCompactPathEx(sb, path, length, 0);
        return sb.ToString();
    }

	private void _onClearRecentFiles_Click(object obj, EventArgs evt)
	{
		try
		{
			RegistryKey rK = Registry.CurrentUser.OpenSubKey(this.SubKeyName, true);
			if (rK == null)
				return;
			string[] values = rK.GetValueNames();
			foreach (string valueName in values)
				rK.DeleteValue(valueName, true);
			rK.Close();
			this.ParentMenuItem.DropDownItems.Clear();
            fullPath.Clear();
			this.ParentMenuItem.Enabled = false;
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.ToString());
		}
		if (OnClearRecentFilesClick != null)
			this.OnClearRecentFilesClick(obj, evt);
	}
		
	private void _refreshRecentFilesMenu()
	{
		RegistryKey rK;
		string s;
		ToolStripItem tSI;
        fullPath.Clear();

		try
		{
			rK = Registry.CurrentUser.OpenSubKey(this.SubKeyName, false);
			if (rK == null)
			{
				this.ParentMenuItem.Enabled = false;
				return;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Cannot open recent files registry key:\n" + ex.ToString());
			return;
		}

		this.ParentMenuItem.DropDownItems.Clear();
		string[] valueNames = rK.GetValueNames();
		foreach (string valueName in valueNames.Reverse())
		{
			s = rK.GetValue(valueName, null) as string;
			if (s == null)
				continue;
            fullPath.Add(s);
            tSI = this.ParentMenuItem.DropDownItems.Add(TruncatePath(s, MAX_FILE_PATH_CHARS));
			tSI.Click += new EventHandler(this.OnRecentFileClick);
		}

		if (this.ParentMenuItem.DropDownItems.Count == 0)
		{
			this.ParentMenuItem.Enabled = false;
			return;
		}
		this.ParentMenuItem.DropDownItems.Add("-");
		tSI = this.ParentMenuItem.DropDownItems.Add("Clear list");
		tSI.Click += new EventHandler(this._onClearRecentFiles_Click);
		this.ParentMenuItem.Enabled = true;
	}
	#endregion

	#region Public members
	public void AddRecentFile(string fileNameWithFullPath)
	{
		string s;
		try
		{
			RegistryKey rK = Registry.CurrentUser.CreateSubKey(this.SubKeyName, RegistryKeyPermissionCheck.ReadWriteSubTree);
			for (int i = 0; true; i++)
			{
				s = rK.GetValue(i.ToString(), null) as string;
				if (s == null)
				{
					rK.SetValue(i.ToString(), fileNameWithFullPath);
					rK.Close();
					break;
				}
				else if (s == fileNameWithFullPath)
				{
					rK.Close();
					break;
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.ToString());
		}
		this._refreshRecentFilesMenu();
	}

	public void RemoveRecentFile(string fileNameWithFullPath)
	{
		try
		{
			RegistryKey rK = Registry.CurrentUser.OpenSubKey(this.SubKeyName, true);
			string[] valuesNames = rK.GetValueNames();
			foreach (string valueName in valuesNames)
			{
				if ((rK.GetValue(valueName, null) as string) == fileNameWithFullPath)
				{
					rK.DeleteValue(valueName, true);
					this._refreshRecentFilesMenu();
					break;
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.ToString());
		}
		this._refreshRecentFilesMenu();
	}

    public string GetFullFilePath(int index)
    {
        if (index < fullPath.Count)
            return fullPath[index];

        return null;
    }
	#endregion

	/// <exception cref="ArgumentException">If anything is null or nameOfProgram contains a forward slash or is empty.</exception>
	public MRUManager(ToolStripMenuItem parentMenuItem, string nameOfProgram, Action<object, EventArgs> onRecentFileClick, Action<object, EventArgs> onClearRecentFilesClick = null)
	{
		if(parentMenuItem == null || onRecentFileClick == null ||
			nameOfProgram == null || nameOfProgram.Length == 0 || nameOfProgram.Contains("\\"))
			throw new ArgumentException("Bad argument.");

		this.ParentMenuItem = parentMenuItem;
		this.NameOfProgram = nameOfProgram;
		this.OnRecentFileClick = onRecentFileClick;
		this.OnClearRecentFilesClick = onClearRecentFilesClick;
		this.SubKeyName = string.Format("Software\\{0}\\MRU", this.NameOfProgram);

		this._refreshRecentFilesMenu();
	}
}
