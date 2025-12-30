interface Address {
      country: string;
      state: string;
      region: string;
      city: string;
      postalCode: string;
      streetAndHouse: string;
}

interface Details {
      name: string;
      phone: string;
      email: string;
      address: Address;
}

interface PackageDetails {
      barcode: string;
      from: Details;
      to: Details;
      weight: number;
      date: string;
      contentDescription: string[];
}

interface Attachment {
      path: string;
      filename: string;
      typeId: string;
      documentFormat: string
}

interface PackageResponse {
      eventId: string;
      attachments: Attachment[];
      packageDetails: PackageDetails;
}
