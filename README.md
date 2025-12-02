# FBX4VRM Conversion

Unity Editor Extension for converting FBX/Prefab to VRM format.

## Features

- FBX/Prefab to VRM 0.x and VRM 1.0 conversion
- lilToon to MToon shader conversion
- Bone structure verification and normalization
- Expression (BlendShape) auto-mapping
- SpringBone/PhysBone to VRM SpringBone conversion
- Multi-angle screenshot capture for bug reporting
- Preset management for conversion settings

## Requirements

- Unity 2021.3 or later
- UniVRM (automatically installed as dependency)

## Installation

### Via Unity Package Manager (Git URL)

1. Open **Window > Package Manager**
2. Click **+ > Add package from git URL...**
3. Enter:
```
https://github.com/dsgarage/FBX4VRMConversion-UPM.git
```

### With specific version

```
https://github.com/dsgarage/FBX4VRMConversion-UPM.git#v0.0.1
```

### Via manifest.json

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.dsgarage.fbx4vrmconversion": "https://github.com/dsgarage/FBX4VRMConversion-UPM.git#v0.0.1"
  }
}
```

## Usage

1. Select a Humanoid FBX or Prefab in the Project window
2. Open **Tools > FBX4VRM > Quick Export**
3. Configure conversion settings
4. Click **Export VRM**

## License

MIT License - see [LICENSE](LICENSE) for details.

## Author

dsgarage - https://github.com/dsgarage
