# Campaign access delegation

User story:

As a fundraiser, I want to create or authorize additional user accounts (delegates) with limited permissions so that I can delegate tasks like posting updates and responding to inquiries without sharing my account, while preventing them from performing restricted actions such as modifying or closing campaigns.

```plantuml
@startuml
left to right direction

actor "Fund Raiser (Owner)" as Owner
actor "Other user (Delegates)" as Delegate

package "Fund Raising Application" {
  usecase "View Delegate List" as ViewDelegates
  usecase "Invite New Delegate" as InviteDelegate
  usecase "Toggle Specific Permissions\n(e.g., Post Updates)" as TogglePerms
  usecase "Remove Delegate Completely" as RemoveDelegate

  usecase "Attempt Authorized Action" as AuthorizedAction
  usecase "Attempt Restricted Action" as RestrictedAction
}


Owner --> ViewDelegates
Owner --> InviteDelegate
Owner --> TogglePerms
Owner --> RemoveDelegate

Delegate --> AuthorizedAction
Delegate ..> RestrictedAction :denied

@enduml
```
