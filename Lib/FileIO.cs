using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace FileIOHelper
{
   ///<summary>Contains various static FileIO-Functionality</summary>
   public static class FileIO
   {
      ///<summary>
      ///<para>Deletes a file or directory on the drive if it exists. Method blocks until the deletion is being performed by the OS (or the timeout is reached).</para>
      ///<para>Note that directories that are read-only will usually end up triggering a timeout when being deleted with this method.</para>
      ///<para>In contrast to Net's default method: When this method returns, the target is actually gone from the drive or we threw an exception.</para>
      ///</summary>
      ///<param name="toDelete">a filepath to a file or directory you want to delete</param>
      ///<param name="timeToWaitForDeletionInSeconds">the amount of time to wait for the OS to actually perform the deletion, defaults to 20 seconds</param>
      ///<exception cref="TimeoutException">if a timeout is reached and the deletion cannot be guaranteed, for instance because another process is using a file inside
      ///this directory or the OS is still processing this operation.</exception>  
      public static void Delete( string toDelete, int timeToWaitForDeletionInSeconds = 20 )
      {
         var p = Path.GetFullPath( toDelete );
         if( File.Exists( p ) || Directory.Exists( p ) )
         {
            var isFile = File.Exists( p );
            var targetIsGone = false;
            var deleteWasScheduled = false;

            #region schedule deletion
            // schedule the file/directory for removal
            int maxMillis = Math.Max( 2, timeToWaitForDeletionInSeconds ) * 1000;
            var lastMillis = Environment.TickCount;
            const int checkInterval = 2000;//we check every two seconds, which not terribly exact but it does not have to be
            var currentMillis = 0;

            while( currentMillis < maxMillis )
            {
               var tickCount = Environment.TickCount;
               currentMillis += ( tickCount - lastMillis );
               lastMillis = tickCount;

               try
               {
                  if( isFile ) File.Delete( p ); else Directory.Delete( p, true );
                  // exception? if no, we are finished;
                  deleteWasScheduled = true;
               }
               catch( Exception )
               {
                  // we ignore the exception, the file/dir is still in use...    perform a wait, then check again.
                  // if the sleep is pushing us over our timeout, we will still perform one last check.
                  if( currentMillis < maxMillis ) Thread.Sleep( checkInterval );
               }

               if( deleteWasScheduled ) break;
            }
            if( !deleteWasScheduled )
            {
               throw new TimeoutException( string.Format( "Failed to schedule deletion of existing {0} '{1}', timeout was {2} seconds.", isFile ? "file" : "directory", p, timeToWaitForDeletionInSeconds ) );
            }
            #endregion schedule deletion

            #region wait for removal
            // Wait for the ACTUAL DELETION, now that it is triggered (the loop above only tells the OS to schedule a deletion).
            // This code is obviously only executed if the loop above did not throw an exeception, aka we do not wait here if
            // we are already in an error.
            lastMillis = Environment.TickCount;
            currentMillis = 0;

            while( currentMillis < maxMillis )
            {
               var tickCount = Environment.TickCount;
               currentMillis += ( tickCount - lastMillis );
               lastMillis = tickCount;

               targetIsGone =  isFile ? !File.Exists( p ) : !Directory.Exists( p );

               if( targetIsGone ) break;
            }
            if( !targetIsGone )
            {
               throw new TimeoutException( string.Format( "Failed to delete existing {0} '{1}' in time after deletion was scheduled, timeout was {2} seconds.", isFile ? "file" : "directory", p, timeToWaitForDeletionInSeconds ) );
            }
            #endregion wait for removal
         }
      }

      ///<summary>
      ///<para>Duplicates a file or directory on the drive at another location. Method blocks until the copy is finished by the OS (or the timeout is reached).</para>
      ///<para>In contrast to the NetFramework's default method: When this method returns, the target is actually fully copied.</para>
      ///</summary>
      ///<param name="source">a filepath to the file or directory you want to copy somewhere else</param>
      ///<param name="destination">a filepath detailing a (usually nonexistent) file or directory, specifying the target destination</param>
      ///<param name="overwrite">if 'false', throw an exception if the target exists, otherwise first delete present data and then copy.</param>
      ///<param name="timeToWaitForCopyInSeconds">max amount of time to wait for the OS to actually perform a single copy operation, defaults to 20</param>
      ///<exception cref="TimeoutException">if the timeout is reached and the copy cannot be guaranteed</exception> 
      ///<exception cref="ArgumentException">if source or target do not exist or are otherwise invalid</exception> 
      public static void Copy( string source, string destination, bool overwrite = false, int timeToWaitForCopyInSeconds = 20 )
      {
         var s = Path.GetFullPath( source );
         var d = Path.GetFullPath( destination );
         var replacerRegex = new Regex( Regex.Escape( s ) );
         Func<string, string> ReplacePath = (string input) => replacerRegex.Replace( input, d, 1 );

         //verfiy input
         if( !File.Exists( s ) && !Directory.Exists( s ) )
         {
            throw new ArgumentException( string.Format( "Given source '{0}' does not exist!", s ) );
         }
         var isFile = File.Exists( s );

         if( isFile && Directory.Exists( d ) || !isFile && File.Exists( d ) )
         {
            throw new ArgumentException( string.Format( "Source and Target must both be a {0}!", isFile ? "file" : "directory" ) );
         }

         if( isFile && File.Exists( d ) || !isFile && Directory.Exists( d ) )
         {
            if( overwrite )
            {
               Delete( d );
            }
            else
            {
               throw new ArgumentException( string.Format( "Failed to create {0} '{1}' at target '{2}' since that target already exists!", isFile ? "file" : "directory", s, d ) );
            }
         }

         // start copy-command
         if( isFile )
         {
            var parentDir = Path.GetDirectoryName( d );
            if( parentDir != null && !Directory.Exists( parentDir ) ) CreateDir( parentDir );
            File.Copy( s, d );
            WaitForFileWrite( d, timeToWaitForCopyInSeconds );
         }
         else
         {
            ActuallyCreateDir( d, timeToWaitForCopyInSeconds );
            var dirs = Directory.GetDirectories( s, "*", SearchOption.AllDirectories );
            Array.Sort( dirs, ( string a, string b ) => a.Length - b.Length );
            foreach( string dirPath in dirs )
            {
               var dest = ReplacePath( dirPath );
               ActuallyCreateDir( dest, timeToWaitForCopyInSeconds );
            }

            foreach( string oldPath in Directory.GetFiles( s, "*.*", SearchOption.AllDirectories ) )
            {
               var newPath = ReplacePath( oldPath );
               File.Copy( oldPath, newPath );
               WaitForFileWrite( newPath, timeToWaitForCopyInSeconds );
            }
         }
      }

      private static void ActuallyCreateDir( string dir, int timeToWait )
      {
         var path = "";
         foreach( var p in dir.Split( Path.DirectorySeparatorChar ) )
         {
            path = ( path.Length == 0 ) ? (p + Path.DirectorySeparatorChar) : Path.Combine( path, p );
            if( File.Exists( path ) )
            {
               throw new ArgumentException( string.Format( //create precise exception for the user
                  "Path '{0}' refers to a file and thus cannot be part of a directory path!", path ) );
            }

            var parentDir = Path.GetDirectoryName( path );
            if( parentDir == null || Directory.Exists( path ) )
            {
               continue; //is file-system's root OR nothing to be done
            }

            var dirExistsNow = false;

            Directory.CreateDirectory( path );

            int maxMillis = Math.Max( 2, timeToWait ) * 1000;
            var lastMillis = Environment.TickCount;
            var currentMillis = 0;
            const int checkInterval = 2000; //we check every two seconds, which not terribly exact but it does not have to be

            while( currentMillis < maxMillis )
            {
               var tickCount = Environment.TickCount;
               currentMillis += ( tickCount - lastMillis );
               lastMillis = tickCount;

               if( Directory.Exists( path ) )
               {
                  dirExistsNow = true;
                  break;
               }
               Thread.Sleep( checkInterval );
            }

            if( !dirExistsNow )
            {
               throw new TimeoutException( string.Format( "Failed to create directory '{0}' in time after deletion was scheduled, timeout was {1} seconds.", path, timeToWait ) );
            }
         }
      }

      private static void WaitForFileWrite( string file, int secondsToWait )
      {
         int maxMillis = Math.Max( 2, secondsToWait ) * 1000;

         FileStream stream = null;
         FileInfo fileInfo = null;

         var lastMillis = Environment.TickCount;
         var currentMillis = 0;
         const int checkInterval = 2000;//we check every two seconds, which not terribly exact but it does not have to be

         while( currentMillis < maxMillis )
         {
            try
            {
               var tickCount = Environment.TickCount;
               currentMillis += ( tickCount - lastMillis );
               lastMillis = tickCount;

               fileInfo = new FileInfo( file );
               if( !fileInfo.Exists )
               {
                  Thread.Sleep( checkInterval );
                  continue;
               }

               // if we got a successful ReadWrite-FileHandle from the OS, the file has been fully copied and can be touched
               // by other programs, ours for example! If there is no exception here, our write was successful.
               stream = fileInfo.Open( FileMode.Open, FileAccess.ReadWrite, FileShare.None );
               stream.Close();

               //exit the loop!
               return;
            }
            catch( Exception )
            {
               //do not leave any half-open streams behind
               if( stream != null )
               {
                  stream.Close();
                  stream = null;
               }

               //we ignore all exceptions and retry, but wait for some time via sleep().
               // if the sleep is pushing us over our timeout, we will still perform one last check.
               if( currentMillis < maxMillis ) Thread.Sleep( checkInterval );
            }
         }

         throw new TimeoutException( string.Format( "Failed to create file '{0}', the file is still 'locked' after {1} seconds. It is either unavailable because it is still being written to or does not even exist at all and its creation is being blocked by something else.", file, secondsToWait ) );
      }

      ///<summary>
      ///<para>Moves a file or directory on the drive at another location. Method blocks until the movement of said target is finished by the OS (or the timeout is reached).</para>
      ///<para>In contrast to Net's default method: When this method returns, the target is actually fully moved to the new location.</para>
      ///</summary>
      ///<param name="source">a filepath to the file or directory you want to move somewhere else</param>
      ///<param name="destination">a filepath detailing a (usually nonexistent) file or directory, specifying the target destination</param>
      ///<param name="timeToWaitForMoveInSeconds">max amount of time to wait for the OS to actually perform a single move operation, defaults to 20</param>
      ///<exception cref="TimeoutException">if the timeout is reached and the move's success cannot be guaranteed</exception> 
      ///<exception cref="ArgumentException">if source does not exit or the target does already exists (no overwrite via move)</exception> 
      public static void Move( string source, string destination, int timeToWaitForMoveInSeconds = 20 )
      {
         Copy( source, destination, false, timeToWaitForMoveInSeconds );
         Delete( source, timeToWaitForMoveInSeconds );
      }

      ///<summary>Ensures all directories and subdirectories in the specified path exist, creating them if necessary.</summary>
      ///<param name="targetPath">directory to create</param>
      ///<param name="timeToWaitInSeconds">max amount of time to wait for the OS to actually create the directory, defaults to 20</param>
      ///<exception cref="TimeoutException">if the creation's success cannot be determined after the timeout</exception> 
      ///<exception cref="ArgumentException">if the path is invalid</exception> 
      public static void CreateDir( string targetPath, int timeToWaitInSeconds = 20 )
      {
         var p = Path.GetFullPath( targetPath );
         if( !Directory.Exists( p ) )
         {
            ActuallyCreateDir( p, timeToWaitInSeconds );
         }
      }

      ///<summary>
      ///<para>Creates a new zero-bytes file under the given path, unless that file already exists!</para>
      ///<para>NOTE: The necessary directory structure is created as well using '<see cref="CreateDir"/>'.</para>
      ///</summary>
      ///<param name="targetFile">a filepath to the file you want to create</param>
      ///<param name="timeToWaitInSeconds">max amount of time to wait for the OS to actually create the directory structure and target file,
      ///defaults to 20</param>
      ///<exception cref="ArgumentException">if the file already exists (we allow no overwrite)</exception> 
      ///<exception cref="ArgumentNullException">if the path is NULL</exception> 
      ///<exception cref="IOException">if the file cannot be created for whatever reason</exception> 
      ///<exception cref="TimeoutException">if the creation's success cannot be determined after the timeout</exception> 
      public static void CreateFile( string targetFile, int timeToWaitInSeconds = 20 )
      {
         ActuallyCreateFile( targetFile, null, timeToWaitInSeconds );
      }

      ///<summary>
      ///<para>Creates a file with the specified string (encoded as UTF-8) under the given path, unless that file already exists!</para>
      ///<para>NOTE: The necessary directory structure is created as well using '<see cref="CreateDir"/>'.</para>
      ///</summary>
      ///<param name="targetFile">a filepath to the file you want to create</param>
      ///<param name="contentToWrite">the content to write into the file</param>
      ///<param name="timeToWaitInSeconds">max amount of time to wait for the OS to actually create the directory structure and target file,
      ///defaults to 20</param>
      ///<exception cref="ArgumentException">if the file already exists (we allow no overwrite)</exception> 
      ///<exception cref="ArgumentNullException">if path or given content is NULL</exception> 
      ///<exception cref="IOException">if the file cannot be created for whatever reason</exception> 
      ///<exception cref="TimeoutException">if the creation's success cannot be determined after the timeout</exception> 
      public static void CreateFile( string targetFile, string contentToWrite, int timeToWaitInSeconds = 20 )
      {
         if( contentToWrite == null ) throw new ArgumentNullException( "String to write cannot be NULL" );
         ActuallyCreateFile( targetFile, Encoding.UTF8.GetBytes( contentToWrite ), timeToWaitInSeconds );
      }

      ///<summary>
      ///<para>Creates a file with the content under the given path, unless that file already exists!</para>
      ///<para>NOTE: The necessary directory structure is created as well using '<see cref="CreateDir"/>'.</para>
      ///</summary>
      ///<param name="targetFile">a filepath to the file you want to create</param>
      ///<param name="contentToWrite">the content to write into the file</param>
      ///<param name="timeToWaitInSeconds">max amount of time to wait for the OS to actually create the directory structure and target file,
      ///defaults to 20</param>
      ///<exception cref="ArgumentException">if the file already exists (we allow no overwrite)</exception> 
      ///<exception cref="ArgumentNullException">if path or given content is NULL</exception> 
      ///<exception cref="IOException">if the file cannot be created for whatever reason</exception> 
      ///<exception cref="TimeoutException">if the creation's success cannot be determined after the timeout</exception> 
      public static void CreateFile( string targetFile, byte[] contentToWrite, int timeToWaitInSeconds = 20 )
      {
         if( contentToWrite == null ) throw new ArgumentNullException( "Content to write cannot be NULL" );
         ActuallyCreateFile( targetFile, contentToWrite, timeToWaitInSeconds );
      }

      private static void ActuallyCreateFile( string targetFile, byte[] contentToWrite, int timeToWaitInSeconds = 20 )
      {
         var p = Path.GetFullPath( targetFile );
         if( File.Exists( p ) )
         {
            throw new ArgumentException( string.Format( "File '{0}' already exists!", p ) );
         }

         var dir = Path.GetDirectoryName( p );
         CreateDir( dir, timeToWaitInSeconds );

         var tmpFile = Path.GetTempFileName();
         if ( contentToWrite != null ) File.WriteAllBytes( tmpFile, contentToWrite );
         Move( tmpFile, p, timeToWaitInSeconds );

         if( !Directory.GetFiles( dir, "*" ).Select( s => s.ToLower() ).Contains( p.ToLower() ) )
         {
            throw new IOException( string.Format( "Failed to detect File '{0}' with full path '{1}' on the drive, although the operation reported a success. The most likely explanation is that the path or filename you entered is invalid for a case-insensitive Windows File System but it tried to cope with it anyway.", targetFile, p ) );
         }
         //no need to verify the write, we were able to MOVE the file after all ;-)
      }

      ///<summary>
      ///<para>Computes an MD5-Hash for a given file.</para>
      ///<para>Note: Obviously, this method reads the contents of this file to do that. Parallel writes might produce unwanted results.</para>
      ///</summary>
      ///<param name="targetFile">a filepath to the file you want to create</param>
      ///<exception cref="ArgumentException">if the file already exists (we allow no overwrite)</exception> 
      ///<exception cref="IOException">if the file cannot be created for whatever reason</exception> 
      ///<exception cref="TimeoutException">if the creation's success cannot be determined after 20 seconds</exception> 
      ///<returns>an upper-case, human-readable MD5-Hash</returns>
      public static string ComputeMd5HashForFile( string targetFile )
      {
         var p = Path.GetFullPath( targetFile );
         if( !File.Exists( p ) )
         {
            throw new ArgumentException( string.Format ("File '{0}' does not exist!", p ) );
         }

         byte[] hash = null;
         using( var md5 = MD5.Create() )
         {
            using( var stream = File.OpenRead( p ) )
            {
               hash = md5.ComputeHash( stream );
            }
         }

         if ( hash == null )
            throw new IOException( string.Format( "Failed to compute a Hash for file '{0}' due to unknown reasons.", targetFile ) );

         // re-encode the bytes into readable a string
         StringBuilder sb = new StringBuilder();
         for( int i = 0; i < hash.Length; i++ )
         {
            sb.Append( hash[i].ToString( "X2" ) );
         }

         return sb.ToString().ToUpper();
      }
   }
}