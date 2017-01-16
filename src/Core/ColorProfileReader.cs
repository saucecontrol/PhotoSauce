using System;
using System.IO;
using System.Text;
using System.Diagnostics.Contracts;

namespace PhotoSauce.MagicScaler
{
	//http://www.color.org/specification/ICC1v43_2010-12.pdf
	internal class ColorProfileInfo
	{
		public string CMM { get; private set; }
		public string Version { get; private set; }
		public string DeviceClass { get; private set; }
		public string DataColorSpace { get; private set; }
		public string PcsColorSpace { get; private set; }
		public string Platform { get; private set; }
		public DateTime CreateDate { get; private set; }
		public string Manufacturer { get; private set; }
		public string Model { get; private set; }
		public string Creator { get; private set; }
		public bool IsValid { get; private set; }

		public bool IsDisplayRgb => DataColorSpace == "RGB " && DeviceClass == "mntr";
		public bool IsCmyk => DataColorSpace == "CMYK";
		public bool IsStandardSrgb => CMM == "Lino" && Manufacturer == "IEC " && Model == "sRGB";

		private void init(Stream stm)
		{
			if (stm.Length < 128)
				return; //throw new InvalidDataException("Invalid ICC profile.  Header is incomplete.");

			using (var rdr = new BinaryReader(stm, Encoding.ASCII))
			{
				uint len = rdr.ReadBigEndianUInt32();
				if (len != rdr.BaseStream.Length)
					return; //throw new InvalidDataException("Invalid ICC profile.  Internal length doesn't match data length.");

				CMM = new string(rdr.ReadChars(4));

				var ver = rdr.ReadBytes(4);
				IsValid = ver[0] == 2 || ver[0] == 4;
				Version = string.Join(".", ver[0], ver[1] >> 4, ver[1] & 0xf, 0);

				DeviceClass = new string(rdr.ReadChars(4));

				DataColorSpace = new string(rdr.ReadChars(4));

				PcsColorSpace = new string(rdr.ReadChars(4));

				CreateDate = new DateTime(rdr.ReadBigEndianUInt16(), rdr.ReadBigEndianUInt16(), rdr.ReadBigEndianUInt16(), rdr.ReadBigEndianUInt16(), rdr.ReadBigEndianUInt16(), rdr.ReadBigEndianUInt16());

				var acsp = rdr.ReadBytes(4);

				Platform = new string(rdr.ReadChars(4));

				var flags = rdr.ReadBytes(4);

				Manufacturer = new string(rdr.ReadChars(4));

				Model = new string(rdr.ReadChars(4));

				var attributes = rdr.ReadBytes(8);

				var intent = rdr.ReadBytes(4);

				var d50xyz = rdr.ReadBytes(12);

				Creator = new string(rdr.ReadChars(4));
			}
		}

		public ColorProfileInfo(byte[] profileData)
		{
			Contract.Requires<ArgumentNullException>(profileData != null, nameof(profileData));

			using (var ms = new MemoryStream(profileData))
				init(ms);
		}

		public ColorProfileInfo(Stream profileData)
		{
			Contract.Requires<ArgumentNullException>(profileData != null, nameof(profileData));

			init(profileData);
		}
	}
}