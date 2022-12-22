/// <summary>
/// Get a test jwt token for Dev testing. To test the endpoints, authorize access by clicking the "Authorize" button above and add the value "Bearer {Token}"
/// </summary>
/// <param name="userName">Temporary UserId (optional)</param>
/// <returns>JWT token</returns>
[AllowAnonymous]
[Route("test/jwttoken")]
[HttpGet]
[ProducesResponseType(typeof(string), (int)HttpStatusCode.OK)]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
public async Task<IActionResult> GenerateJwtToken(string userName = "test")
{
	// generate token that is valid for 7 days
	var tokenHandler = new JwtSecurityTokenHandler();
	var key = Encoding.ASCII.GetBytes(_config["Auth:Jwt:Key"]);
	var tokenDescriptor = new SecurityTokenDescriptor
	{
		Subject = new ClaimsIdentity(new[] { new Claim("id", userName) }),
		Expires = DateTime.UtcNow.AddHours(1),
		Issuer = _config["Auth:Jwt:Issuer"],
		Audience = _config["Auth:Jwt:Audience"],
		SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
	};
	var token = tokenHandler.CreateToken(tokenDescriptor);
	var strToken = tokenHandler.WriteToken(token);
	return Ok(strToken);
}