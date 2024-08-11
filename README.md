# __AAK__ integration for the __Kinematic Character Controller__

- [Action Adventure Kit](https://assetstore.unity.com/packages/templates/systems/action-adventure-kit-217284)  
- [Kinematic Character Controller](https://assetstore.unity.com/packages/tools/physics/kinematic-character-controller-99131)

This repository contains an implementation of MovementBase that uses a Kinematic Character Motor. It is called __KinematicMovement__ and can act as a direct replacement for the CharacterControllerMovement that is included in AAK.

## Setup

The packages are not included in this repository and have to be downloaded separately. After cloning the project will be missing the KinematicCharacterController and SoftLeitner folders.  
![project structure](https://github.com/Schossi/AAK_KinematicCharacterController/blob/main/Project.png)  
Start by downloading them from the asset store or copy them from another project before opening this one to avoid all the errors from missing dependencies.

There are some additional steps to make the souls example scene work.
- add a reference to the AdventureKinematic assembly to AdventureSouls  
![project structure](https://github.com/Schossi/AAK_KinematicCharacterController/blob/main/AssemblyReference.png)
- change the movement of the SoulsPlayerCharacter to a KinematicMovement  
![project structure](https://github.com/Schossi/AAK_KinematicCharacterController/blob/main/SoulsPlayerCharacter.png)

## Scenes

- Minimal  
No fuss example of how the KinematicMovement can be used inside a GenericCharacter.

- Mixamo  
Character from mixamo that is moved using root motion.

- Souls
Variant of the general debug scenes from the souls demo but the movement of the character has been replaced with the KinematicMovement.

- Hero
Same as Souls but for the hero demo
