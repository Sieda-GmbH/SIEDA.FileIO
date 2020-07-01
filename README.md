# SIEDA.FileIO

A simple helper library, wrapping around Net's basic File-IO functionality. All code is stateless and contained withing the static class **FileIO**.
In contrast to Net's default methods, this implementation is a **blocking one**, all methods only return when the full success of the operation has been verified.

## Available functionality:

Different variants exist for most of these methods and some additional functionality has not been listed here, but the following gives you a quick overview of "the highlights":

```csharp
   public static class FileIO
   {
      //Deletes a file or directory on the drive if it exists. Method blocks until the deletion is being performed by the OS (or the timeout is reached).   
      public static void Delete( string toDelete, int timeToWaitForDeletionInSeconds = 20 ) { /* ... */ }

      //Duplicates a file or directory on the drive at another location. Method blocks until the copy is finished by the OS (or the timeout is reached).
      public static void Copy( string source, string destination, bool overwrite = false, int timeToWaitForCopyInSeconds = 20 ) { /* ... */ }

      //Moves a file or directory on the drive at another location. Method blocks until the movement of said target is finished by the OS (or the timeout is reached).
      public static void Move( string source, string destination, int timeToWaitForMoveInSeconds = 20 ) { /* ... */ }

      //Ensures all directories and subdirectories in the specified path exist, creating them if necessary.
      public static void CreateDir( string targetPath, int timeToWaitInSeconds = 20 ) { /* ... */ }

      //Creates a file with the specified string (encoded as UTF-8) under the given path, unless that file already exists!
      public static void CreateFile( string targetFile, string contentToWrite, int timeToWaitInSeconds = 20 ) { /* ... */ }
   }
```