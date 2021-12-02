using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SIEDA.FileIO
{
   ///<summary>Contains various static FileIO-Functionality</summary>
   public static class IOHelper
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
         Exception lastException = null;
         if( File.Exists( p ) || Directory.Exists( p ) )
         {
            var isFile = File.Exists( p );
            var targetIsGone = false;
            var deleteWasScheduled = false;

            #region schedule deletion
            // schedule the file/directory for removal
            int maxMillis = Math.Max( 2, timeToWaitForDeletionInSeconds ) * 1000;
            var lastMillis = Environment.TickCount;
            const int checkInterval = 2000;//we check every two seconds, which is not terribly exact but it does not have to be
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
               catch( SecurityException e ) //makes no sense waiting for permissions, that is NOT an IO-issue that will fix itself by waiting...
               {
                  throw new IOException( $"Insufficient permissions to access '{p}', cannot perform deletion!", e );
               }
               catch( UnauthorizedAccessException e ) //makes no sense waiting for permissions, that is NOT an IO-issue that will fix itself by waiting...
               {
                  throw new IOException( $"Insufficient permissions to access '{p}', cannot perform deletion!", e );
               }
               catch( Exception e )
               {
                  lastException = e;

                  // we ignore the exception, the file/dir is still in use...    perform a wait, then check again.
                  // if the sleep is pushing us over our timeout, we will still perform one last check.
                  if( currentMillis < maxMillis ) Thread.Sleep( checkInterval );
               }

               if( deleteWasScheduled ) break;
            }
            if( !deleteWasScheduled )
            {
               throw new TimeoutException( $"Failed to schedule deletion of existing { ( isFile ? "file" : "directory" )} '{p}', timeout was {timeToWaitForDeletionInSeconds} seconds.", lastException );
            }
            #endregion schedule deletion

            #region wait for removal
            maxMillis = maxMillis - currentMillis + checkInterval; //an approximation of the remaining time

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
               else Thread.Sleep( checkInterval );
            }
            if( !targetIsGone )
            {
               throw new TimeoutException( $"Failed to delete existing { ( isFile ? "file" : "directory" )} '{p}' in time after deletion was scheduled, timeout was {timeToWaitForDeletionInSeconds} seconds." );
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
      ///<exception cref="IOException">if any other IO-related issue occurs, such as missing permissions for the operation.</exception>
      public static void Copy( string source, string destination, bool overwrite = false, int timeToWaitForCopyInSeconds = 20 )
      {
         var s = Path.GetFullPath( source );
         var d = Path.GetFullPath( destination );
         var replacerRegex = new Regex( Regex.Escape( s ) );
         Func<string, string> ReplacePath = (string input) => replacerRegex.Replace( input, d, 1 );

         //verfiy input
         if( !File.Exists( s ) && !Directory.Exists( s ) )
         {
            throw new ArgumentException( $"Given source '{s}' does not exist!" );
         }
         var isFile = File.Exists( s );

         if( isFile && Directory.Exists( d ) || !isFile && File.Exists( d ) )
         {
            throw new ArgumentException( $"Source and Target must both be a { ( isFile ? "file" : "directory" ) }!" );
         }

         if( isFile && File.Exists( d ) || !isFile && Directory.Exists( d ) )
         {
            if( overwrite )
            {
               Delete( d );
            }
            else
            {
               throw new ArgumentException( $"Failed to create {(isFile ? "file" : "directory")} '{s}' at target '{d}' since that target already exists!" );
            }
         }

         // start copy-command
         if( isFile )
         {
            var parentDir = Path.GetDirectoryName( d );
            if( parentDir != null && !Directory.Exists( parentDir ) ) EnsureDirExists( parentDir );
            try
            {
               File.Copy( s, d );
            }
            catch( Exception e )
            {
               throw new IOException( $"Failed to copy '{s}' to '{d}'.", e );
            }
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
               try{
                  File.Copy( oldPath, newPath );
               }
               catch( Exception e )
               {
                  throw new IOException( $"Failed to copy '{oldPath}' to '{newPath}'.", e );
               }
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
               throw new ArgumentException( //create precise exception for the user
                  $"Path '{path}' refers to a file and thus cannot be part of a directory path!" );
            }

            var parentDir = Path.GetDirectoryName( path );
            if( parentDir == null || Directory.Exists( path ) )
            {
               continue; //is file-system's root OR nothing to be done
            }

            var dirExistsNow = false;

            try
            {
               Directory.CreateDirectory( path );
            }
            catch( Exception e )
            {
               throw new IOException( $"Failed to create directory '{path}'", e );
            }

            int maxMillis = Math.Max( 2, timeToWait ) * 1000;
            var lastMillis = Environment.TickCount;
            var currentMillis = 0;
            const int checkInterval = 2000; //we check every two seconds, which is not terribly exact but it does not have to be

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
               throw new TimeoutException( $"Failed to create directory '{path}' in time after deletion was scheduled, timeout was {timeToWait} seconds." );
            }
         }
      }

      private static void WaitForFileWrite( string file, int secondsToWait )
      {
         int maxMillis = Math.Max( 2, secondsToWait ) * 1000;

         FileStream stream = null;
         FileInfo fileInfo;

         var lastMillis = Environment.TickCount;
         var currentMillis = 0;
         const int checkInterval = 2000;//we check every two seconds, which is not terribly exact but it does not have to be

         Exception lastException = null;

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
            catch( Exception e )
            {
               lastException = e;

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

         throw new TimeoutException( $"Failed to create file '{file}', the file is still 'locked' after {secondsToWait} seconds. It is either unavailable because it is still being written to or does not even exist at all and its creation is being blocked by something else.", lastException );
      }

      ///<summary>
      ///<para>Moves a file or directory on the drive at another location. Method blocks until the movement of said target is finished by the OS (or the timeout is reached).</para>
      ///<para>In contrast to Net's default method: When this method returns, the target is actually fully moved to the new location.</para>
      ///</summary>
      ///<param name="source">a filepath to the file or directory you want to move somewhere else</param>
      ///<param name="destination">a filepath detailing a (usually nonexistent) file or directory, specifying the target destination</param>
      ///<param name="timeToWaitForMoveInSeconds">max amount of time to wait for the OS to actually perform a single move operation, defaults to 20</param>
      ///<exception cref="TimeoutException">if the timeout is reached and the move's success cannot be guaranteed</exception> 
      ///<exception cref="IOException">if any other IO-related issue occurs, such as missing permissions for the operation.</exception>
      public static void Move( string source, string destination, int timeToWaitForMoveInSeconds = 20 )
      {
         Copy( source, destination, false, timeToWaitForMoveInSeconds );
         Delete( source, timeToWaitForMoveInSeconds );
      }

      ///<summary>
      ///<para>Ensures the specified directory-path exists.</para>
      ///<para>This method recursively creates all required sub-directories to achieve this goal.</para>
      ///</summary>
      ///<param name="targetPath">directory to create</param>
      ///<param name="timeToWaitInSeconds">max amount of time to wait for the OS to actually create the directory, defaults to 20</param>
      ///<exception cref="TimeoutException">if the creation's success cannot be determined after the timeout</exception> 
      ///<exception cref="ArgumentException">if the path is invalid</exception> 
      ///<exception cref="IOException">if any other IO-related issue occurs, such as missing permissions for the operation.</exception>
      public static void EnsureDirExists( string targetPath, int timeToWaitInSeconds = 20 )
      {
         var p = Path.GetFullPath( targetPath );
         if( !Directory.Exists( p ) )
         {
            ActuallyCreateDir( p, timeToWaitInSeconds );
         }
      }
      ///<summary>
      ///<para>Ensures the specified directory-path exists *and* is fresh, eliminating any existing directory if necessary.</para>
      ///<para>This method recursively creates all required sub-directories to achieve this goal, but only the specified directory is replaced if necessary.</para>
      ///</summary>
      ///<param name="targetPath">directory to recreate</param>
      ///<param name="timeToWaitInSeconds">max amount of time to wait for the OS to actually create the directory, defaults to 20</param>
      ///<exception cref="TimeoutException">if the creation's success cannot be determined after the timeout</exception> 
      ///<exception cref="ArgumentException">if the path is invalid</exception> 
      ///<exception cref="IOException">if any other IO-related issue occurs, such as missing permissions for the operation.</exception>
      public static void CreateDirAnew( string targetPath, int timeToWaitInSeconds = 30 )
      {
         var p = Path.GetFullPath( targetPath );
         if( Directory.Exists( p ) )
         {
            Delete( p );
         }
         ActuallyCreateDir( p, timeToWaitInSeconds );
      }

      ///<summary>
      ///<para>Creates a new zero-bytes file under the given path, unless that file already exists!</para>
      ///<para>NOTE: The necessary directory structure is created as well using '<see cref="EnsureDirExists"/>'.</para>
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
         ActuallyCreateFile( targetFile, null, false, timeToWaitInSeconds );
      }

      ///<summary>
      ///<para>Creates a new zero-bytes file under the given path, eliminating any previous file at this location!</para>
      ///<para>NOTE: The necessary directory structure is created as well using '<see cref="EnsureDirExists"/>'.</para>
      ///</summary>
      ///<param name="targetFile">a filepath to the file you want to create</param>
      ///<param name="timeToWaitInSeconds">max amount of time to wait for the OS to actually create the directory structure and target file,
      ///defaults to 20</param> 
      ///<exception cref="ArgumentNullException">if the path is NULL</exception> 
      ///<exception cref="IOException">if the file cannot be created for whatever reason</exception> 
      ///<exception cref="TimeoutException">if the creation's success cannot be determined after the timeout</exception> 
      public static void CreateFileAnew( string targetFile, int timeToWaitInSeconds = 20 )
      {
         ActuallyCreateFile( targetFile, null, true, timeToWaitInSeconds );
      }

      ///<summary>
      ///<para>Creates a file with the specified string (encoded as UTF-8) under the given path, unless that file already exists!</para>
      ///<para>NOTE: The necessary directory structure is created as well using '<see cref="EnsureDirExists"/>'.</para>
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
         ActuallyCreateFile( targetFile, Encoding.UTF8.GetBytes( contentToWrite ), false, timeToWaitInSeconds );
      }

      ///<summary>
      ///<para>Creates a file with the specified string (encoded as UTF-8) under the given path, eliminating any previous file at this location!</para>
      ///<para>NOTE: The necessary directory structure is created as well using '<see cref="EnsureDirExists"/>'.</para>
      ///</summary>
      ///<param name="targetFile">a filepath to the file you want to create</param>
      ///<param name="contentToWrite">the content to write into the file</param>
      ///<param name="timeToWaitInSeconds">max amount of time to wait for the OS to actually create the directory structure and target file,
      ///defaults to 20</param>
      ///<exception cref="ArgumentNullException">if path or given content is NULL</exception> 
      ///<exception cref="IOException">if the file cannot be created for whatever reason</exception> 
      ///<exception cref="TimeoutException">if the creation's success cannot be determined after the timeout</exception> 
      public static void CreateFileAnew( string targetFile, string contentToWrite, int timeToWaitInSeconds = 20 )
      {
         if( contentToWrite == null ) throw new ArgumentNullException( "String to write cannot be NULL" );
         ActuallyCreateFile( targetFile, Encoding.UTF8.GetBytes( contentToWrite ), true, timeToWaitInSeconds );
      }

      ///<summary>
      ///<para>Creates a file with the content under the given path, unless that file already exists!</para>
      ///<para>NOTE: The necessary directory structure is created as well using '<see cref="EnsureDirExists"/>'.</para>
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
         ActuallyCreateFile( targetFile, contentToWrite, false, timeToWaitInSeconds );
      }

      ///<summary>
      ///<para>Creates a file with the content under the given path, eliminating any previous file at this location!</para>
      ///<para>NOTE: The necessary directory structure is created as well using '<see cref="EnsureDirExists"/>'.</para>
      ///</summary>
      ///<param name="targetFile">a filepath to the file you want to create</param>
      ///<param name="contentToWrite">the content to write into the file</param>
      ///<param name="timeToWaitInSeconds">max amount of time to wait for the OS to actually create the directory structure and target file,
      ///defaults to 20</param>s
      ///<exception cref="ArgumentNullException">if path or given content is NULL</exception> 
      ///<exception cref="IOException">if the file cannot be created for whatever reason</exception> 
      ///<exception cref="TimeoutException">if the creation's success cannot be determined after the timeout</exception> 
      public static void CreateFileAnew( string targetFile, byte[] contentToWrite, int timeToWaitInSeconds = 20 )
      {
         if( contentToWrite == null ) throw new ArgumentNullException( "Content to write cannot be NULL" );
         ActuallyCreateFile( targetFile, contentToWrite, true, timeToWaitInSeconds );
      }

      private static void ActuallyCreateFile( string targetFile, byte[] contentToWrite, bool overwrite, int timeToWaitInSeconds)
      {
         var p = Path.GetFullPath( targetFile );
         if( File.Exists( p ) )
         {
            if( overwrite )
            {
               Delete( p, timeToWaitInSeconds );
            }
            else
            {
               throw new ArgumentException( $"File '{p}' already exists!" );
            }
         }

         var dir = Path.GetDirectoryName( p );
         EnsureDirExists( dir, timeToWaitInSeconds );

         var tmpFile = Path.GetTempFileName();
         try
         {
            if( contentToWrite != null ) File.WriteAllBytes( tmpFile, contentToWrite );
         }
         catch( Exception e )
         {
            throw new IOException( $"Failed to write into file '{tmpFile}', which is to be moved to '{p}' afterwards.", e );
         }
         Move( tmpFile, p, timeToWaitInSeconds );

         if( !Directory.GetFiles( dir, "*" ).Select( s => s.ToLower() ).Contains( p.ToLower() ) )
         {
            throw new IOException( $"Failed to detect File '{p}' on the drive, although the operation success was confirmed by the OS. The most likely explanation is that the path or filename you entered is invalid for your File System but it tried to cope with it anyway." );
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
      ///<exception cref="IOException">if any other IO-related issue occurs, such as missing permissions for the operation.</exception>
      ///<returns>an upper-case, human-readable MD5-Hash</returns>
      public static string ComputeMd5HashForFile( string targetFile )
      {
         byte[] hash = null;
         try
         {
            var p = Path.GetFullPath( targetFile );
            if( !File.Exists( p ) )
            {
               throw new ArgumentException( $"File '{p}' does not exist!" );
            }
            using( var md5 = MD5.Create() )
            {
               using( var stream = File.OpenRead( p ) )
               {
                  hash = md5.ComputeHash( stream );
               }
            }
         }
         catch( Exception e )
         {
            throw new IOException( $"Failed to access '{targetFile}'", e );
         }

         if ( hash == null )
            throw new IOException( $"Failed to compute a Hash for file '{targetFile}' due to unknown reasons." );

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