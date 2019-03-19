module VsToBazel.Files

open System
open System.IO

let read (path : string) = async {
  use sr = new StreamReader (path)
  return!
    sr.ReadToEndAsync()
    |> Async.AwaitTask
}

let write (path : string) (content : string) = async {
  use sw = new StreamWriter (path)
  return!
    sw.WriteAsync(content)
    |> Async.AwaitTask
}

let touch (path : string) =
  use myFileStream = File.Open (path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite)
  myFileStream.Close ()
  File.SetLastWriteTimeUtc (path, DateTime.UtcNow)
