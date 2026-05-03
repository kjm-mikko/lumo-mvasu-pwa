import { bootstrapApplication } from '@angular/platform-browser';
import config from 'devextreme/core/config';

import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';
import { licenseKey } from './.devextreme/license-key';

config({ licenseKey });

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));
