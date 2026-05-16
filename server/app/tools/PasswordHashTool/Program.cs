using System.Text;
using Yggdrasil.Security.Hashing;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
	Console.Error.WriteLine("Usage: PasswordHashTool <password>");
	return 1;
}

var password = args[0];
var md5Password = MD5.Encode(password);
var hash = BCrypt.HashPassword(md5Password, BCrypt.GenerateSalt());
var hashHex = Convert.ToHexString(Encoding.UTF8.GetBytes(hash));

Console.WriteLine(hash);
Console.WriteLine(hashHex);
Console.WriteLine(BCrypt.CheckPassword(md5Password, hash) ? "OK" : "FAIL");

return 0;
