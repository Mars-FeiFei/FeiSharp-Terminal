using Microsoft.Win32;
using System;
public class FileAssociation
{
    public static void RegisterFileAssociation(string fileExtension, string applicationPath)
    {
        string extKey = $@"HKEY_CLASSES_ROOT\{fileExtension}";
        string appKey = $@"HKEY_CLASSES_ROOT\MyFSCFile";


        if (Registry.GetValue(extKey, "", null) == null)
        {

            Registry.SetValue(extKey, "", "MyFSCFile");
        }


        if (Registry.GetValue(appKey, "", null) == null)
        {

            Registry.SetValue(appKey, "", "FSC File Program");


            string command = $"\"{applicationPath}\" \"%1\"";
            Registry.SetValue($@"{appKey}\shell\open\command", "", command);
        }
        else
        {

            Console.WriteLine("The file association is already registered.");
        }
    }
}