using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace SIEDA.FileIO.Testing
{
   /** Ugly integration Tests */
   public class FileIOTestsuite
   {
      private string testDir;

      private const string testFile = "testfile.txt";

      private static readonly string[] testFilesWithRelativePath = { testFile, $".{Path.DirectorySeparatorChar}{testFile}" };

      [SetUp]
      public void SetUp()
      {
         // It is rather hard to test functionality that is designed to setup and write tests :-( and get rid of IO-Raceconditions...
         // Since we require our functionality-under-test for our tests, we verify the correct initial state before starting our test!

         testDir = Path.Combine( Path.GetTempPath(), "FileIOTestsuite" );
         Exception exception = null;

         try { IOHelper.Delete( testDir ); } catch( Exception e ) { exception = exception == null ? e : exception; }
         try { Directory.Delete( testDir, true ); } catch( Exception e ) { exception = exception == null ? e : exception; }
         if( Directory.Exists( testDir ) ) throw new Exception( "TestSetup failed, could not delete previous integration-test directory!", exception );

         foreach( var file in testFilesWithRelativePath )
         {
            try { IOHelper.Delete( file ); } catch( Exception e ) { exception = exception == null ? e : exception; }
            try { File.Delete( file ); } catch( Exception e ) { exception = exception == null ? e : exception; }
            if( File.Exists( file ) ) throw new Exception( $"TestSetup failed, could not delete previous integration-test file '{file}'!", exception );
         }

         try { IOHelper.EnsureDirExists( testDir ); } catch( Exception e ) { exception = exception == null ? e : exception; }
         try { Directory.CreateDirectory( testDir ); } catch( Exception e ) { exception = exception == null ? e : exception; }
         if( !Directory.Exists( testDir ) ) throw new Exception( "TestSetup failed, could not create integration-test directory!", exception );
      }

      [Test]
      public void CreateDir()
      {
         var dir = Path.Combine( Path.Combine( Path.GetFullPath( testDir ), "CreateDir" ), "my little directory with spaces" );
         IOHelper.CreateDir( dir );

         Assert.That( Directory.Exists( dir ), Is.True );
      }

      [Test]
      public void CreateDirAnew()
      {
         var dir = Path.Combine( Path.Combine( Path.GetFullPath( testDir ), "CreateDirAnew" ), "somedir" );
         IOHelper.CreateDir( dir );
         var file = Path.Combine( dir, "myfile.txt" );
         IOHelper.CreateFile( file );
         Assert.That( File.Exists( file ), Is.True );

         IOHelper.CreateDirAnew( dir );
         Assert.That( File.Exists( file ), Is.False );
      }

      [Test]
      public void CreateDirDoesNotReplace()
      {
         var dir = Path.Combine( Path.Combine( Path.GetFullPath( testDir ), "CreateDirDoesNotReplace" ), "my little directory with spaces" );
         IOHelper.CreateDir( dir );
         Assert.Throws<ArgumentException>( () => IOHelper.CreateDir( dir ) );
      }

      [Test]
      public void EnsureDirExists()
      {
         var dir = Path.Combine( Path.Combine( Path.GetFullPath( testDir ), "CreateDir" ), "my little directory with spaces" );
         IOHelper.EnsureDirExists( dir );

         Assert.That( Directory.Exists( dir ), Is.True );
      }

      [Test]
      public void InvalidPath()
      {
         var existingDir = Path.Combine( testDir, "ForInvalidPathTest" );
         IOHelper.EnsureDirExists( existingDir );
         IOHelper.CreateFile( Path.Combine( existingDir, "testfile.txt" ) );

         var dirForTest = Path.Combine( testDir, "InvalidPath" );
         IOHelper.EnsureDirExists( dirForTest );

         Func<string, string> MakeInput = (string casename) =>
            Path.Combine( Path.Combine( dirForTest, casename ), "3k%f:4,h<.(f{d{s;@k#^h&j!" );

         Assert.Throws<IOException>( () => IOHelper.CreateDir( MakeInput( "CreateDir" ) ) );
         Assert.Throws<IOException>( () => IOHelper.CreateDirAnew( MakeInput( "CreateDirAnew" ) ) );
         Assert.Throws<IOException>( () => IOHelper.EnsureDirExists( MakeInput( "EnsureDirExists" ) ) );
         Assert.Throws<IOException>( () => IOHelper.CreateFile( MakeInput( "CreateFile" ) ) );
         Assert.Throws<IOException>( () => IOHelper.CreateFileAnew( MakeInput( "CreateFileAnew" ) ) );
         Assert.Throws<ArgumentException>( () => IOHelper.Move( MakeInput( "MoveFrom" ), existingDir ) );
         Assert.Throws<ArgumentException>( () => IOHelper.Copy( MakeInput( "CreateFrom" ), existingDir ) );
         Assert.Throws<IOException>( () => IOHelper.Move( existingDir, MakeInput( "MoveTo" ) ) );
         Assert.Throws<IOException>( () => IOHelper.Copy( existingDir, MakeInput( "CopyTo" ) ) );

         IOHelper.Delete( MakeInput( "Delete" ) ); //throws nothing because the input can NEVER exist on a Windows FileSystem
      }

      [Test]
      public void DeleteDir()
      {
         var dir = Path.Combine( testDir, "DeleteDir" );
         IOHelper.EnsureDirExists( dir );
         var file = Path.Combine( dir, "testfile.txt" );
         IOHelper.CreateFile( file );

         IOHelper.Delete( dir );
         Assert.That( Directory.Exists( dir ), Is.False ); //file is obviously gone as well
      }

      [Test]
      public void DeleteFile()
      {
         var dir = Path.Combine( testDir, "DeleteFile" );
         IOHelper.EnsureDirExists( dir );
         var file = Path.Combine( dir, "testfile.txt" );
         IOHelper.CreateFile( file );

         IOHelper.Delete( file );
         Assert.That( File.Exists( dir ), Is.False ); //file is gone...
         Assert.That( Directory.Exists( dir ), Is.True ); //...but directory remains
      }

      [Test]
      public void DeleteNonexistingDir()
      {
         var dir = Path.Combine( Path.Combine( testDir, "Nonexisting" ), "AlsoNonExisting" );
         IOHelper.Delete( dir );
         Assert.That( Directory.Exists( dir ), Is.False );
      }

      [Test]
      public void CreateAndDeleteFile()
      {
         var dir = Path.Combine( testDir, "CreateAndDeleteFile" );
         var file1 = Path.Combine( dir, "testfile.txt" );
         var file2 = Path.Combine( dir, "my little testfile with spaces.txt" );

         IOHelper.CreateFile( file1 );
         Assert.That( File.Exists( file1 ), Is.True );
         IOHelper.CreateFile( file2 );
         Assert.That( File.Exists( file2 ), Is.True );


         IOHelper.Delete( file1 );
         Assert.That( Directory.Exists( dir ), Is.True ); //dir not touched
         Assert.That( File.Exists( file1 ), Is.False );
         Assert.That( File.Exists( file2 ), Is.True ); //other file not touched

         IOHelper.Delete( file2 );
         Assert.That( Directory.Exists( dir ), Is.True );
         Assert.That( File.Exists( file2 ), Is.False );
      }

      [Test]
      public void CreateFileDoesNotReplace()
      {
         var dir = Path.Combine( testDir, "CreateFileDoesNotReplace" );
         var file = Path.Combine( dir, "testfile.txt" );
         IOHelper.CreateFile( file, new byte[] { 1, 2, 3, 4, 5 } );

         Assert.That( File.Exists( file ), Is.True ); // new directory and file now exist!
         Assert.Throws<ArgumentException>( () => IOHelper.CreateFile( file, new byte[] { 6, 7, 8, 9 } ) );
         Assert.That( new byte[] { 1, 2, 3, 4, 5 }, Is.EquivalentTo( File.ReadAllBytes( file ) ) );
      }

      [Test]
      public void CreateFileAnew()
      {
         var dir = Path.Combine( testDir, "CreateFileAnew" );
         var file = Path.Combine( dir, "testfile.txt" );
         IOHelper.CreateFile( file, new byte[] { 1,2,3,4,5 } );
         IOHelper.CreateFileAnew( file, new byte[] { 6, 7, 8, 9 } );

         Assert.That( File.Exists( file ), Is.True ); // new directory and file now exist!
         Assert.That( new byte[] { 6, 7, 8, 9 }, Is.EquivalentTo( File.ReadAllBytes(file) ) );
      }

      [Test]
      public void MoveFileFromAtoB()
      {
         var dirA = Path.Combine( Path.Combine( testDir, "MoveFileFromAtoB" ), "a" );
         var dirB = Path.Combine( Path.Combine( testDir, "MoveFileFromAtoB" ), "b" );

         var fileA = Path.Combine( dirA, "testfile.txt" );
         var fileB = Path.Combine( dirB, "testfile.txt" );

         IOHelper.EnsureDirExists( dirA );
         IOHelper.CreateFile( fileA );
         //B does not exist, not even partially!

         IOHelper.Move( fileA, fileB );

         Assert.That( File.Exists( fileA ), Is.False ); //file was moved
         Assert.That( Directory.Exists( dirA ), Is.True ); //directory was not touched
         Assert.That( File.Exists( fileB ), Is.True ); // new directory and file now exist!
      }

      [Test]
      public void CopyFileFromAtoB()
      {
         var dirA = Path.Combine( Path.Combine( testDir, "CopyFileFromAtoB" ), "a" );
         var dirB = Path.Combine( Path.Combine( testDir, "CopyFileFromAtoB" ), "b" );

         var fileA = Path.Combine( dirA, "testfile.txt" );
         var fileB = Path.Combine( dirB, "testfile.txt" );

         IOHelper.EnsureDirExists( dirA );
         IOHelper.CreateFile( fileA );
         //B does not exist!

         IOHelper.Copy( fileA, fileB );

         Assert.That( File.Exists( fileA ), Is.True ); //file was copied, old file still exists
         Assert.That( Directory.Exists( dirA ), Is.True ); //directory was not touched either
         Assert.That( File.Exists( fileB ), Is.True ); // new directory and file now exist!
      }

      [Test]
      public void MoveDirFromAtoB()
      {
         var dirA = Path.Combine( Path.Combine( testDir, "MoveDirFromAtoB" ), "a" );
         var subDirA = Path.Combine( dirA, "sub dir" );
         var dirB = Path.Combine( Path.Combine( testDir, "MoveDirFromAtoB" ), "b" );
         var subDirB = Path.Combine( dirB, "sub dir" );

         var fileA = Path.Combine( dirA, "testfile1.txt" );
         var fileB = Path.Combine( dirB, "testfile1.txt" );
         var subfileA = Path.Combine( subDirA, "testfile2.txt" );
         var subfileB = Path.Combine( subDirB, "testfile2.txt" );

         IOHelper.EnsureDirExists( subDirA );
         IOHelper.CreateFile( fileA );
         IOHelper.CreateFile( subfileA );
         //B does not exist, not even partially!

         IOHelper.Move( dirA, dirB );

         Assert.That( File.Exists( fileA ), Is.False ); //file was moved
         Assert.That( Directory.Exists( dirA ), Is.False ); //old directory does not exist anymore

         Assert.That( Directory.Exists( dirB ), Is.True ); // new directory exists...
         Assert.That( File.Exists( fileB ), Is.True ); // ...and has the right content...
         Assert.That( File.Exists( subfileB ), Is.True ); // ...including the nested one
      }

      [Test]
      public void CopyWithReplace()
      {
         var dirA = Path.Combine( Path.Combine( testDir, "CopyInPartExist" ), "a" );
         var subDirA1 = Path.Combine( dirA, "subdir1" );
         var subDirA2 = Path.Combine( dirA, "sub dir2" );
         var dirB = Path.Combine( Path.Combine( testDir, "CopyInPartExist" ), "b" );
         var subDirB1 = Path.Combine( dirB, "subdir1" );
         var subDirB2 = Path.Combine( dirB, "sub dir2" );

         var fileA = Path.Combine( dirA, "upperfile.txt" );
         var fileB = Path.Combine( dirB, "upperfile.txt" );
         var subfileA1 = Path.Combine( subDirA1, "testfile.txt" );
         var subfileB1 = Path.Combine( subDirB1, "testfile.txt" );
         var subfileA2 = Path.Combine( subDirA2, "testfile II.txt" );
         var subfileB2 = Path.Combine( subDirB2, "testfile II.txt" );

         var oldFileInB = Path.Combine( subDirB1, "oldfile.txt" );

         IOHelper.EnsureDirExists( subDirA1 );
         IOHelper.CreateFile( fileA );
         IOHelper.CreateFile( subfileA1 );
         IOHelper.CreateFile( subfileA2 );

         IOHelper.EnsureDirExists( subDirB1 );   // B's dir-structure exists partially and...
         IOHelper.CreateFile( oldFileInB );// ...has an existing file in there!

         IOHelper.Copy( dirA, dirB, true /* must replace */ );

         Assert.That( Directory.Exists( dirA ), Is.True ); // old directory exists...
         Assert.That( File.Exists( fileA ), Is.True ); // ...and has the right content...
         Assert.That( File.Exists( subfileA1 ), Is.True ); // ...including first...
         Assert.That( File.Exists( subfileA2 ), Is.True ); // ...and second nested ones.

         Assert.That( Directory.Exists( dirB ), Is.True ); // new directory exists...
         Assert.That( File.Exists( fileB ), Is.True ); // ...and has the right content...
         Assert.That( File.Exists( subfileB1 ), Is.True ); // ...including first...
         Assert.That( File.Exists( subfileB2 ), Is.True ); // ...and second nested ones.

         Assert.That( File.Exists( oldFileInB ), Is.False ); // however, previous content in B was overwritten!
      }

      [Test]
      public void CopyThrowsOnExisting()
      {
         var dirSource = Path.Combine( Path.Combine( testDir, "CopyThrowsOnExisting" ), "source" );
         var dirTarget = Path.Combine( Path.Combine( testDir, "CopyThrowsOnExisting" ), "target" );

         var sourceFile = Path.Combine( dirSource, "testfile.txt" );
         var targetFile = Path.Combine( dirTarget, "testfile.txt" );

         IOHelper.CreateFile( sourceFile );
         IOHelper.CreateFile( targetFile );

         Assert.Throws<ArgumentException>( () => IOHelper.Copy( sourceFile, targetFile ) );
      }

      [Test]
      public void MoveThrowsOnExisting()
      {
         var dirSource = Path.Combine( Path.Combine( testDir, "MoveThrowsOnExisting" ), "source" );
         var dirTarget = Path.Combine( Path.Combine( testDir, "MoveThrowsOnExisting" ), "target" );

         var sourceFile = Path.Combine( dirSource, "testfile.txt" );
         var targetFile = Path.Combine( dirTarget, "testfile.txt" );

         IOHelper.CreateFile( sourceFile );
         IOHelper.CreateFile( targetFile );

         Assert.Throws<ArgumentException>( () => IOHelper.Move( sourceFile, targetFile ) );
      }

      [Test]
      public void CopyThrowsOnMissing()
      {
         var dirSource = Path.Combine( Path.Combine( testDir, "CopyThrowsOnExisting" ), "source" );
         var dirTarget = Path.Combine( Path.Combine( testDir, "CopyThrowsOnExisting" ), "target" );

         var sourceFile = Path.Combine( dirSource, "testfile.txt" );
         var targetFile = Path.Combine( dirTarget, "testfile.txt" );

         Assert.Throws<ArgumentException>( () => IOHelper.Copy( sourceFile, targetFile ) );
      }

      [Test]
      public void MoveThrowsOnMissing()
      {
         var dirSource = Path.Combine( Path.Combine( testDir, "MoveThrowsOnMissing" ), "source" );
         var dirTarget = Path.Combine( Path.Combine( testDir, "MoveThrowsOnMissing" ), "target" );

         var sourceFile = Path.Combine( dirSource, "testfile.txt" );
         var targetFile = Path.Combine( dirTarget, "testfile.txt" );

         Assert.Throws<ArgumentException>( () => IOHelper.Move( sourceFile, targetFile ) );
      }

      [Test]
      public void CopyThrowsOnMismatch()
      {
         var dirSource = Path.Combine( Path.Combine( testDir, "CopyThrowsOnMismatch" ), "source" );
         var dirTarget = Path.Combine( Path.Combine( testDir, "CopyThrowsOnMismatch" ), "target" );

         var sourceFile = Path.Combine( dirSource, "testfile.txt" );
         var targetFile = Path.Combine( dirTarget, "testfile2.txt" );

         IOHelper.CreateFile( sourceFile );
         IOHelper.CreateFile( targetFile );

         Assert.Throws<ArgumentException>( () => IOHelper.Copy( sourceFile, dirTarget ) );
         Assert.Throws<ArgumentException>( () => IOHelper.Copy( dirSource, targetFile ) );
      }

      [Test]
      public void MoveThrowsOnMismatch()
      {
         var dirSource = Path.Combine( Path.Combine( testDir, "MoveThrowsOnMismatch" ), "source" );
         var dirTarget = Path.Combine( Path.Combine( testDir, "MoveThrowsOnMismatch" ), "target" );

         var sourceFile = Path.Combine( dirSource, "testfile.txt" );
         var targetFile = Path.Combine( dirTarget, "testfile2.txt" );

         IOHelper.CreateFile( sourceFile );
         IOHelper.CreateFile( targetFile );

         Assert.Throws<ArgumentException>( () => IOHelper.Move( sourceFile, dirTarget ) );
         Assert.Throws<ArgumentException>( () => IOHelper.Move( dirSource, targetFile ) );
      }


      [Test]
      public void ComputeHashForZeroBytesFile()
      {
         var file = Path.Combine( Path.Combine( testDir, "ComputeHashForZeroBytesFile" ), "testfile.txt" );
         IOHelper.CreateFile( file );

         Assert.That( IOHelper.ComputeMd5HashForFile( file ), Is.EqualTo( "D41D8CD98F00B204E9800998ECF8427E" ) );
      }

      [Test]
      public void ComputeHashForIdenticalFiles()
      {
         var file1 = Path.Combine( Path.Combine( testDir, "ComputeHashForIdenticalFiles" ), "testfile1.txt" );
         var file2 = Path.Combine( Path.Combine( testDir, "ComputeHashForIdenticalFiles" ), "testfile2.txt" );
         IOHelper.CreateFile( file1 );
         IOHelper.CreateFile( file2 );
         File.WriteAllText( file1, "Hubba" );
         File.WriteAllText( file2, "Hubba" );

         var commonHash = "9D745C023AB2C7CAC44557BFB9E5AC8C";
         Assert.That( IOHelper.ComputeMd5HashForFile( file1 ), Is.EqualTo( commonHash ) );
         Assert.That( IOHelper.ComputeMd5HashForFile( file2 ), Is.EqualTo( commonHash ) );
      }

      [Test]
      public void ComputeHashForDifferentFiles()
      {
         var file1 = Path.Combine( Path.Combine( testDir, "ComputeHashForDifferentFiles" ), "testfile1.txt" );
         var file2 = Path.Combine( Path.Combine( testDir, "ComputeHashForDifferentFiles" ), "testfile2.txt" );
         IOHelper.CreateFile( file1 );
         IOHelper.CreateFile( file2 );
         File.WriteAllText( file1, "Hubba" );
         File.WriteAllText( file2, "Hubba-Hub" );

         //differences in content means differences in hash!
         Assert.That( IOHelper.ComputeMd5HashForFile( file1 ), Is.Not.EqualTo( IOHelper.ComputeMd5HashForFile( file2 ) ) );
      }

      [Test]
      public void CreateFile_AbsolutePath()
      {
         var file = Path.Combine( Path.Combine( testDir, "CreateFile" ), "testfile.txt" );
         var teststring = "hallo world";

         IOHelper.CreateFile( file, teststring );

         Assert.That( File.Exists( file ), Is.True );
      }

      public static IEnumerable<TestCaseData> TestCases_CreateFile_RelativePath => testFilesWithRelativePath.Select( file => new TestCaseData( file ) );

      [TestCaseSource( typeof( FileIOTestsuite ), "TestCases_CreateFile_RelativePath" )]
      public void CreateFile_RelativePath( string file )
      {
         var teststring = "hallo world";

         IOHelper.CreateFile( file, teststring );

         Assert.That( File.Exists( file ), Is.True, $"File {file} should have been created, but does not exist!" );
      }

      [Test]
      public void CreateFile_DifferentEncodings()
      {
         var file1 = Path.Combine( Path.Combine( testDir, "CreateFile" ), "testfile1.txt" );
         var file2 = Path.Combine( Path.Combine( testDir, "CreateFile" ), "testfile2.txt" );
         var teststring = "hallo world";

         IOHelper.CreateFile( file1, teststring );
         IOHelper.CreateFile( file2, Encoding.BigEndianUnicode.GetBytes( teststring ) );

         Assert.That( File.Exists( file1 ), Is.True );
         Assert.That( File.Exists( file2 ), Is.True );
         Assert.AreNotEqual( File.ReadAllBytes(file1), File.ReadAllBytes( file2 ),
            "How can the bytes of two files written with different encoding be equal?");
      }
   }
}