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

         try { FileIO.Delete( testDir ); } catch( Exception e ) { exception = exception == null ? e : exception; }
         try { Directory.Delete( testDir, true ); } catch( Exception e ) { exception = exception == null ? e : exception; }
         if( Directory.Exists( testDir ) ) throw new Exception( "TestSetup failed, could not delete previous integration-test directory!", exception );

         foreach( var file in testFilesWithRelativePath )
         {
            try { FileIO.Delete( file ); } catch( Exception e ) { exception = exception == null ? e : exception; }
            try { File.Delete( file ); } catch( Exception e ) { exception = exception == null ? e : exception; }
            if( File.Exists( file ) ) throw new Exception( $"TestSetup failed, could not delete previous integration-test file '{file}'!", exception );
         }

         try { FileIO.CreateDir( testDir ); } catch( Exception e ) { exception = exception == null ? e : exception; }
         try { Directory.CreateDirectory( testDir ); } catch( Exception e ) { exception = exception == null ? e : exception; }
         if( !Directory.Exists( testDir ) ) throw new Exception( "TestSetup failed, could not create integration-test directory!", exception );
      }

      [Test]
      public void CreateDir()
      {
         var dir = Path.Combine( Path.Combine( Path.GetFullPath( testDir ), "CreateDir" ), "my little directory with spaces" );
         FileIO.CreateDir( dir );

         Assert.That( Directory.Exists( dir ), Is.True );
      }

      [Test]
      public void InvalidPath()
      {
         var existingDir = Path.Combine( testDir, "ForInvalidPathTest" );
         FileIO.CreateDir( existingDir );
         FileIO.CreateFile( Path.Combine( existingDir, "testfile.txt" ) );

         var dirForTest = Path.Combine( testDir, "InvalidPath" );
         FileIO.CreateDir( dirForTest );

         Func<string, string> MakeInput = (string casename) =>
            Path.Combine( Path.Combine( dirForTest, casename ), "3k%f:4,h<.(f{d{s;@k#^h&j!" );

         Assert.Throws<IOException>( () => FileIO.CreateDir( MakeInput( "CreateDir" ) ) );
         Assert.Throws<IOException>( () => FileIO.CreateFile( MakeInput( "CreateFile" ) ) );
         Assert.Throws<ArgumentException>( () => FileIO.Move( MakeInput( "MoveFrom" ), existingDir ) );
         Assert.Throws<ArgumentException>( () => FileIO.Copy( MakeInput( "CreateFrom" ), existingDir ) );
         Assert.Throws<IOException>( () => FileIO.Move( existingDir, MakeInput( "MoveTo" ) ) );
         Assert.Throws<IOException>( () => FileIO.Copy( existingDir, MakeInput( "CopyTo" ) ) );

         FileIO.Delete( MakeInput( "Delete" ) ); //throws nothing because the input can NEVER exist on a Windows FileSystem
      }

      [Test]
      public void DeleteDir()
      {
         var dir = Path.Combine( testDir, "DeleteDir" );
         FileIO.CreateDir( dir );
         var file = Path.Combine( dir, "testfile.txt" );
         FileIO.CreateFile( file );

         FileIO.Delete( dir );
         Assert.That( Directory.Exists( dir ), Is.False ); //file is obviously gone as well
      }

      [Test]
      public void DeleteFile()
      {
         var dir = Path.Combine( testDir, "DeleteFile" );
         FileIO.CreateDir( dir );
         var file = Path.Combine( dir, "testfile.txt" );
         FileIO.CreateFile( file );

         FileIO.Delete( file );
         Assert.That( File.Exists( dir ), Is.False ); //file is gone...
         Assert.That( Directory.Exists( dir ), Is.True ); //...but directory remains
      }

      [Test]
      public void DeleteNonexistingDir()
      {
         var dir = Path.Combine( Path.Combine( testDir, "Nonexisting" ), "AlsoNonExisting" );
         FileIO.Delete( dir );
         Assert.That( Directory.Exists( dir ), Is.False );
      }

      [Test]
      public void CreateAndDeleteFile()
      {
         var dir = Path.Combine( testDir, "CreateAndDeleteFile" );
         var file1 = Path.Combine( dir, "testfile.txt" );
         var file2 = Path.Combine( dir, "my little testfile with spaces.txt" );

         FileIO.CreateFile( file1 );
         Assert.That( File.Exists( file1 ), Is.True );
         FileIO.CreateFile( file2 );
         Assert.That( File.Exists( file2 ), Is.True );


         FileIO.Delete( file1 );
         Assert.That( Directory.Exists( dir ), Is.True ); //dir not touched
         Assert.That( File.Exists( file1 ), Is.False );
         Assert.That( File.Exists( file2 ), Is.True ); //other file not touched

         FileIO.Delete( file2 );
         Assert.That( Directory.Exists( dir ), Is.True );
         Assert.That( File.Exists( file2 ), Is.False );
      }

      [Test]
      public void CreateDoesNotOverwrite()
      {
         var dir = Path.Combine( testDir, "CreateDoesNotOverwrite" );
         var file = Path.Combine( dir, "testfile.txt" );
         FileIO.CreateFile( file, new byte[] { 1, 2, 3, 4, 5 } );

         Assert.That( File.Exists( file ), Is.True ); // new directory and file now exist!
         Assert.Throws<ArgumentException>( () => FileIO.CreateFile( file, new byte[] { 6, 7, 8, 9 } ) );
         Assert.That( new byte[] { 1, 2, 3, 4, 5 }, Is.EquivalentTo( File.ReadAllBytes( file ) ) );
      }

      [Test]
      public void CreateAnew()
      {
         var dir = Path.Combine( testDir, "CreateDoesNotOverwrite" );
         var file = Path.Combine( dir, "testfile.txt" );
         FileIO.CreateFile( file, new byte[] { 1,2,3,4,5 } );
         FileIO.CreateFileAnew( file, new byte[] { 6, 7, 8, 9 } );

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

         FileIO.CreateDir( dirA );
         FileIO.CreateFile( fileA );
         //B does not exist, not even partially!

         FileIO.Move( fileA, fileB );

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

         FileIO.CreateDir( dirA );
         FileIO.CreateFile( fileA );
         //B does not exist!

         FileIO.Copy( fileA, fileB );

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

         FileIO.CreateDir( subDirA );
         FileIO.CreateFile( fileA );
         FileIO.CreateFile( subfileA );
         //B does not exist, not even partially!

         FileIO.Move( dirA, dirB );

         Assert.That( File.Exists( fileA ), Is.False ); //file was moved
         Assert.That( Directory.Exists( dirA ), Is.False ); //old directory does not exist anymore

         Assert.That( Directory.Exists( dirB ), Is.True ); // new directory exists...
         Assert.That( File.Exists( fileB ), Is.True ); // ...and has the right content...
         Assert.That( File.Exists( subfileB ), Is.True ); // ...including the nested one
      }

      [Test]
      public void CopyWithOverwrite()
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

         FileIO.CreateDir( subDirA1 );
         FileIO.CreateFile( fileA );
         FileIO.CreateFile( subfileA1 );
         FileIO.CreateFile( subfileA2 );

         FileIO.CreateDir( subDirB1 );   // B's dir-structure exists partially and...
         FileIO.CreateFile( oldFileInB );// ...has an existing file in there!

         FileIO.Copy( dirA, dirB, true /* must overwrite */ );

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

         FileIO.CreateFile( sourceFile );
         FileIO.CreateFile( targetFile );

         Assert.Throws<ArgumentException>( () => FileIO.Copy( sourceFile, targetFile ) );
      }

      [Test]
      public void MoveThrowsOnExisting()
      {
         var dirSource = Path.Combine( Path.Combine( testDir, "MoveThrowsOnExisting" ), "source" );
         var dirTarget = Path.Combine( Path.Combine( testDir, "MoveThrowsOnExisting" ), "target" );

         var sourceFile = Path.Combine( dirSource, "testfile.txt" );
         var targetFile = Path.Combine( dirTarget, "testfile.txt" );

         FileIO.CreateFile( sourceFile );
         FileIO.CreateFile( targetFile );

         Assert.Throws<ArgumentException>( () => FileIO.Move( sourceFile, targetFile ) );
      }

      [Test]
      public void CopyThrowsOnMissing()
      {
         var dirSource = Path.Combine( Path.Combine( testDir, "CopyThrowsOnExisting" ), "source" );
         var dirTarget = Path.Combine( Path.Combine( testDir, "CopyThrowsOnExisting" ), "target" );

         var sourceFile = Path.Combine( dirSource, "testfile.txt" );
         var targetFile = Path.Combine( dirTarget, "testfile.txt" );

         Assert.Throws<ArgumentException>( () => FileIO.Copy( sourceFile, targetFile ) );
      }

      [Test]
      public void MoveThrowsOnMissing()
      {
         var dirSource = Path.Combine( Path.Combine( testDir, "MoveThrowsOnMissing" ), "source" );
         var dirTarget = Path.Combine( Path.Combine( testDir, "MoveThrowsOnMissing" ), "target" );

         var sourceFile = Path.Combine( dirSource, "testfile.txt" );
         var targetFile = Path.Combine( dirTarget, "testfile.txt" );

         Assert.Throws<ArgumentException>( () => FileIO.Move( sourceFile, targetFile ) );
      }

      [Test]
      public void CopyThrowsOnMismatch()
      {
         var dirSource = Path.Combine( Path.Combine( testDir, "CopyThrowsOnMismatch" ), "source" );
         var dirTarget = Path.Combine( Path.Combine( testDir, "CopyThrowsOnMismatch" ), "target" );

         var sourceFile = Path.Combine( dirSource, "testfile.txt" );
         var targetFile = Path.Combine( dirTarget, "testfile2.txt" );

         FileIO.CreateFile( sourceFile );
         FileIO.CreateFile( targetFile );

         Assert.Throws<ArgumentException>( () => FileIO.Copy( sourceFile, dirTarget ) );
         Assert.Throws<ArgumentException>( () => FileIO.Copy( dirSource, targetFile ) );
      }

      [Test]
      public void MoveThrowsOnMismatch()
      {
         var dirSource = Path.Combine( Path.Combine( testDir, "MoveThrowsOnMismatch" ), "source" );
         var dirTarget = Path.Combine( Path.Combine( testDir, "MoveThrowsOnMismatch" ), "target" );

         var sourceFile = Path.Combine( dirSource, "testfile.txt" );
         var targetFile = Path.Combine( dirTarget, "testfile2.txt" );

         FileIO.CreateFile( sourceFile );
         FileIO.CreateFile( targetFile );

         Assert.Throws<ArgumentException>( () => FileIO.Move( sourceFile, dirTarget ) );
         Assert.Throws<ArgumentException>( () => FileIO.Move( dirSource, targetFile ) );
      }


      [Test]
      public void ComputeHashForZeroBytesFile()
      {
         var file = Path.Combine( Path.Combine( testDir, "ComputeHashForZeroBytesFile" ), "testfile.txt" );
         FileIO.CreateFile( file );

         Assert.That( FileIO.ComputeMd5HashForFile( file ), Is.EqualTo( "D41D8CD98F00B204E9800998ECF8427E" ) );
      }

      [Test]
      public void ComputeHashForIdenticalFiles()
      {
         var file1 = Path.Combine( Path.Combine( testDir, "ComputeHashForIdenticalFiles" ), "testfile1.txt" );
         var file2 = Path.Combine( Path.Combine( testDir, "ComputeHashForIdenticalFiles" ), "testfile2.txt" );
         FileIO.CreateFile( file1 );
         FileIO.CreateFile( file2 );
         File.WriteAllText( file1, "Hubba" );
         File.WriteAllText( file2, "Hubba" );

         var commonHash = "9D745C023AB2C7CAC44557BFB9E5AC8C";
         Assert.That( FileIO.ComputeMd5HashForFile( file1 ), Is.EqualTo( commonHash ) );
         Assert.That( FileIO.ComputeMd5HashForFile( file2 ), Is.EqualTo( commonHash ) );
      }

      [Test]
      public void ComputeHashForDifferentFiles()
      {
         var file1 = Path.Combine( Path.Combine( testDir, "ComputeHashForDifferentFiles" ), "testfile1.txt" );
         var file2 = Path.Combine( Path.Combine( testDir, "ComputeHashForDifferentFiles" ), "testfile2.txt" );
         FileIO.CreateFile( file1 );
         FileIO.CreateFile( file2 );
         File.WriteAllText( file1, "Hubba" );
         File.WriteAllText( file2, "Hubba-Hub" );

         //differences in content means differences in hash!
         Assert.That( FileIO.ComputeMd5HashForFile( file1 ), Is.Not.EqualTo( FileIO.ComputeMd5HashForFile( file2 ) ) );
      }

      [Test]
      public void CreateFile_AbsolutePath()
      {
         var file = Path.Combine( Path.Combine( testDir, "CreateFile" ), "testfile.txt" );
         var teststring = "hallo world";

         FileIO.CreateFile( file, teststring );

         Assert.That( File.Exists( file ), Is.True );
      }

      public static IEnumerable<TestCaseData> TestCases_CreateFile_RelativePath => testFilesWithRelativePath.Select( file => new TestCaseData( file ) );

      [TestCaseSource( typeof( FileIOTestsuite ), "TestCases_CreateFile_RelativePath" )]
      public void CreateFile_RelativePath( string file )
      {
         var teststring = "hallo world";

         FileIO.CreateFile( file, teststring );

         Assert.That( File.Exists( file ), Is.True, $"File {file} should have been created, but does not exist!" );
      }

      [Test]
      public void CreateFile_DifferentEncodings()
      {
         var file1 = Path.Combine( Path.Combine( testDir, "CreateFile" ), "testfile1.txt" );
         var file2 = Path.Combine( Path.Combine( testDir, "CreateFile" ), "testfile2.txt" );
         var teststring = "hallo world";

         FileIO.CreateFile( file1, teststring );
         FileIO.CreateFile( file2, Encoding.BigEndianUnicode.GetBytes( teststring ) );

         Assert.That( File.Exists( file1 ), Is.True );
         Assert.That( File.Exists( file2 ), Is.True );
         Assert.AreNotEqual( File.ReadAllBytes(file1), File.ReadAllBytes( file2 ),
            "How can the bytes of two files written with different encoding be equal?");
      }
   }
}