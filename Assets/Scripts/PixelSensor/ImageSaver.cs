using System;
using System.IO;
using UnityEngine;

public static class ImageSaver
{

    public static void SaveImage()
    {
        try
        {
            // Application.persistentDataPath path is --> /storage/emulated/0/Android/data/com.magicleap.unity.examples/files/
            var strDataPath = Path.Combine(Application.persistentDataPath, "FILENAME.FMT");
            File.WriteAllText(strDataPath, "Hello world");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("File writing error ::" + ex);
        }
    }
    public static void SaveTestFile()
    {
        try
        {
            // Application.persistentDataPath path is --> /storage/emulated/0/Android/data/com.magicleap.unity.examples/files/
            var strDataPath = Path.Combine(Application.persistentDataPath, "SaveFile.txt");
            File.WriteAllText(strDataPath, "Hello world");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("File writing error ::" + ex);
        }
    }
    // public void SaveImageToDownloads(Context context, byte[] imageData, string fileName)
    // {
    //     // Define file metadata
    //     ContentValues values = new ContentValues();
    //     values.Put(MediaStore.MediaColumns.DisplayName, fileName); // File name
    //     values.Put(MediaStore.MediaColumns.MimeType, "image/raw"); // MIME type
    //     values.Put(MediaStore.MediaColumns.RelativePath, Environment.DirectoryDownloads); // Downloads folder
    //
    //     // Insert the file into MediaStore
    //     var resolver = context.ContentResolver;
    //     var uri = resolver.Insert(MediaStore.Downloads.ExternalContentUri, values);
    //
    //     if (uri != null)
    //     {
    //         using (var outputStream = resolver.OpenOutputStream(uri))
    //         {
    //             outputStream.Write(imageData, 0, imageData.Length); // Write the raw image data
    //         }
    //     }
    // }

}