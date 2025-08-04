using System.ComponentModel.DataAnnotations;

namespace TripWiseAPI.Models.DTO
{
    public class SignupRequest
    {
        [Required(ErrorMessage = "Tên đăng nhập không được để trống.")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Email không được để trống.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mật khẩu không được để trống.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Xác nhận mật khẩu không được để trống.")]
        [Compare("Password", ErrorMessage = "Mật khẩu và xác nhận mật khẩu không khớp.")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "SignupRequestId không được để trống.")]
        public string SignupRequestId { get; set; }

        public override string ToString()
        {
            return $"Username: {Username}, Email: {Email}, SignupRequestId: {SignupRequestId}";
        }
    }
}
