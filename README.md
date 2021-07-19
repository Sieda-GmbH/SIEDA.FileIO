# SIEDA.FileIO

A simple helper library, wrapping around Net's basic File-IO functionality. All code is stateless and contained within the static class **IOHelper**.
In contrast to Net's default methods this implementation is a **blocking one**, all methods only return when the full success of the operation has been verified.

## What is this for?

Most file-systems will just receive e.g. your command to copy a file to some place and decide, independently from your C# program, when **exactly** execute this operation shall be executed. Note that 99% of the time, this is perfectly fine as this behavior results in way better performance for your daily operations.

But in some rare use-cases, especially test-setup related ones, you can run into IO-related races when interleaving IO and other code semantics. This library is intended for these rare use-cases, during which you want to ensure an IO-operation has actually been completed before continuing to do something else.

Do not use this library when performance is critical, your file-systems is doing the things it does for a reason!

## Where can I get/download it?

You can find a NuGet-Package at [www.nuget.org](https://www.nuget.org/packages/SIEDA.FileIO/), containing binaries for different frameworks. There are no special dependencies.

## Available functionality:

Different variants exist for most of these methods and some additional functionality has not been listed here, but the following gives you a quick overview of "the highlights":

```csharp
   public static class IOHelper
   {
      //Deletes a file or directory on the drive if it exists. Method blocks until the deletion is being performed by the OS (or the timeout is reached).   
      public static void Delete( string toDelete, int timeToWaitForDeletionInSeconds = 20 ) { /* ... */ }

      //Duplicates a file or directory on the drive at another location. Method blocks until the copy is finished by the OS (or the timeout is reached).
      public static void Copy( string source, string destination, bool overwrite = false, int timeToWaitForCopyInSeconds = 20 ) { /* ... */ }

      //Moves a file or directory on the drive at another location. Method blocks until the movement of said target is finished by the OS (or the timeout is reached).
      public static void Move( string source, string destination, int timeToWaitForMoveInSeconds = 20 ) { /* ... */ }

      //Ensures all directories and subdirectories in the specified path exist, creating them if necessary.
      public static void EnsureDirExists( string targetPath, int timeToWaitInSeconds = 20 ) { /* ... */ }

      //Creates a file with the specified string (encoded as UTF-8) under the given path, unless that file already exists!
      public static void CreateFile( string targetFile, string contentToWrite, int timeToWaitInSeconds = 20 ) { /* ... */ }
   }
```
