Feature: Send e-mail
  As a client application
  I want to submit an e-mail through the service
  So that recipients receive my message via the SMTP infrastructure

  Scenario: Successfully sending a valid e-mail
    Given a sender "sales@example.com"
    And a recipient "customer@example.com"
    And the subject "Your order shipped"
    And the body "Thanks for your purchase!"
    When the e-mail is submitted
    Then the e-mail is delivered
    And the delivery has a provider message id

  Scenario: Rejecting an e-mail with an invalid recipient
    Given a sender "sales@example.com"
    And a recipient "not-a-valid-address"
    And the subject "Hi"
    And the body "Body"
    When the e-mail is submitted
    Then the submission is rejected
    And no e-mail is dispatched

  Scenario: Rejecting an e-mail without a subject
    Given a sender "sales@example.com"
    And a recipient "customer@example.com"
    And the subject ""
    And the body "Body"
    When the e-mail is submitted
    Then the submission is rejected
