# TMS API Versioning & Deprecation Policy

This document defines what constitutes a breaking change and outlines the deprecation process for the Training Management System (TMS) API.

---

## 1. Classification of Changes

Every schema or behavioral change on the API must be categorized as either **Breaking** or **Additive**.

### Breaking Changes (Requires Major Version Bump)
Any change that forces a client application code update to prevent runtime compilation or execution failure:
* **Structural Deletions:** Removing or renaming a JSON field, resource, or endpoint.
* **Type/Format Alterations:** Changing the data type of an existing field (e.g., integer to string).
* **Behavioral Shifts:** Changing the default sort order of a resource collection or modifying HTTP success/error status codes.
* **Validation Tightening:** Adding new validation rules or making previously optional fields required.

### Additive Changes (Non-Breaking / Safe for Patches)
Safe modifications that preserve backward compatibility and require no immediate client updates:
* **Additions:** Adding a new optional or nullable JSON field to an existing response payload.
* **New Routes:** Registering an entirely new API endpoint.
* **New Options:** Adding a new optional query parameter or header support.

---

## 2. Sunset Window (Grace Period)

* To accommodate rural training centers operating on quarterly maintenance and deployment windows, old major versions are guaranteed to run for a **minimum of 6 months** after a successor major version ships.

---

## 3. Deprecation Protocol & Communication

When a major version is flagged for retirement, the team executes a dual programmatic and human notification strategy:

### Programmatic Signaling (From Day 1 of Successor Release)
Every response returned by the deprecated endpoint must include these three HTTP headers:
* `Deprecation: true` (Signals the version is officially deprecated)
* `Sunset: [UTC Date]` (Specifies the absolute shutdown date/time)
* `Link: <[Successor-URL]>; rel="successor-version"` (Directs clients to the equivalent endpoint on the new API version)

### Human/Operational Channels
* **Changelog:** A clear entry added to the project's root `CHANGELOG.md`.
* **Direct Notifications:** An automated email warning dispatched to all registered API key holders.
* **Calendar Invite:** A shared calendar invite sent to partner integration teams marking the official V1 shutdown date.

---

## 4. Version Skipping

* Clients are **never** forced to upgrade sequentially through intermediate versions (e.g., migrating from V1 to V2, then V2 to V3). 
* Direct migrations (e.g., leaping from V1 directly to V3) are fully supported.
