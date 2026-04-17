namespace POSI.Domain.Exceptions;

public class DuplicateEmailException : Exception
{
    public DuplicateEmailException() : base("El email ya está registrado.") { }
}

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Email o contraseña incorrectos.") { }
}

public class InvalidRefreshTokenException : Exception
{
    public InvalidRefreshTokenException() : base("Token de refresco inválido o expirado.") { }
}
