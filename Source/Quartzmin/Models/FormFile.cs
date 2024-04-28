namespace Quartzmin.Models
{
    public class FormFile
    {
        private readonly IFormFile _file;
        public FormFile(IFormFile file) => _file = file;

        public Stream GetStream() => _file.OpenReadStream();

        public byte[] GetBytes()
        {
            using (var stream = new MemoryStream())
            {
                GetStream().CopyTo(stream);
                return stream.ToArray();
            }
        }
    }
}
